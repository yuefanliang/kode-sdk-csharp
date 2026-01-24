using System.Collections.Concurrent;

namespace Kode.Agent.Sdk.Infrastructure.Sandbox;

/// <summary>
/// Docker-based sandbox implementation.
/// Commands execute inside a dedicated container; file operations happen on the host (restricted by sandbox boundary)
/// and are exposed to the container via volume mounts.
///
/// Key security model:
/// - "Command isolation": shell commands are executed in Docker (not on the host).
/// - "Workspace exposure": the sandbox working directory is bind-mounted into the container (rw).
///   This means commands can still modify/delete files under the mounted paths, but cannot directly touch the rest of
///   the host filesystem unless you explicitly mount more paths.
/// - "Network isolation": default Docker network mode is "none" (configurable via SandboxOptions.DockerNetworkMode).
/// </summary>
public sealed class DockerSandbox : ISandbox
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
    private const string SandboxStateMountPath = "/sandbox_state";

    private readonly IDockerRunner _dockerRunner;
    private readonly string _workingDirectory;
    private readonly string _stateDirectoryHost;
    private readonly string _stateDirectoryContainerBase;
    private readonly Dictionary<string, string> _environmentVariables;
    private readonly TimeSpan _defaultTimeout;
    private readonly bool _enforceBoundary;
    private readonly IReadOnlyList<string> _allowPaths;
    private readonly string _dockerImage;
    private readonly string _dockerNetworkMode;

    // Host path -> container path mount table used by the sandbox container.
    // We mount the sandbox working directory at /workspace, and additional allowPaths (outside working directory)
    // under /mnt/allow{n}.
    private readonly IReadOnlyList<(string HostPath, string ContainerPath)> _volumeMounts;

    // Docker container name for this sandbox instance. Once started, all commands are executed via `docker exec`.
    private string? _containerName;

    // Job/process tracking:
    // - We return `ProcessId` to callers as an internal job id (NOT a host PID).
    // - Each job corresponds to a PID inside the container, plus host-side log files.
    private int _nextJobId;
    private readonly ConcurrentDictionary<int, BackgroundJob> _jobs = new();

    public string SandboxId { get; }
    public string WorkingDirectory => _workingDirectory;

    private sealed class BackgroundJob
    {
        public required int JobId { get; init; }
        public required string Command { get; init; }
        public required int ContainerPid { get; init; }
        public required string ContainerWorkingDirectory { get; init; }
        public required DateTime StartedAt { get; init; }
        public DateTime? EndedAt { get; set; }
        public int? ExitCode { get; set; }
        public required string StdoutPath { get; init; }
        public required string StderrPath { get; init; }
        public required string ExitCodePath { get; init; }
    }

    private DockerSandbox(SandboxOptions? options, IDockerRunner dockerRunner)
    {
        SandboxId = Guid.NewGuid().ToString("N");
        _dockerRunner = dockerRunner;
        _workingDirectory = Path.GetFullPath(options?.WorkingDirectory ?? Environment.CurrentDirectory);
        _environmentVariables = options?.EnvironmentVariables ?? [];
        _defaultTimeout = options?.Timeout ?? DefaultTimeout;
        _enforceBoundary = options?.EnforceBoundary ?? true;
        _allowPaths = (options?.AllowPaths ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Path.GetFullPath)
            .Distinct(GetPathComparer())
            .ToArray();

        _dockerImage = string.IsNullOrWhiteSpace(options?.DockerImage) ? "ubuntu:latest" : options!.DockerImage!;
        _dockerNetworkMode = string.IsNullOrWhiteSpace(options?.DockerNetworkMode) ? "none" : options!.DockerNetworkMode!;

        // Sandbox internal state directory:
        // - Used for DockerSandbox job logs/exit codes (stdout.txt/stderr.txt/exitcode.txt).
        // - Defaults to "<workingDirectory>/.kode" to preserve old behavior.
        // - Can be set to a per-session directory (e.g. "<storeDir>/<agentId>") to keep workspace clean.
        _stateDirectoryHost = string.IsNullOrWhiteSpace(options?.SandboxStateDirectory)
            ? Path.GetFullPath(Path.Combine(_workingDirectory, ".kode"))
            : Path.GetFullPath(options!.SandboxStateDirectory!);

        // Volume mounts strategy:
        // - Always mount sandbox working directory at /workspace (rw)
        // - Mount allowPaths that are OUTSIDE working directory at /mnt/allow{n} (rw)
        //   (paths inside working directory are already covered by the /workspace mount)
        //
        // Later, when we execute a command, its host working directory is mapped to the corresponding container path.
        var mounts = new List<(string HostPath, string ContainerPath)>
        {
            (_workingDirectory, "/workspace")
        };

        var allowIndex = 0;
        foreach (var allow in _allowPaths)
        {
            if (IsUnder(_workingDirectory, allow))
            {
                continue;
            }

            mounts.Add((allow, $"/mnt/allow{allowIndex}"));
            allowIndex++;
        }

        // Mount sandbox state directory if it is outside the working directory.
        // This allows the container to write logs/exitcode files without polluting the workspace mount.
        if (!IsUnder(_workingDirectory, _stateDirectoryHost))
        {
            mounts.Add((_stateDirectoryHost, SandboxStateMountPath));
            _stateDirectoryContainerBase = SandboxStateMountPath;
        }
        else
        {
            // State directory is already covered by the /workspace mount.
            _stateDirectoryContainerBase = MapHostPathToContainerWorkingDir(_stateDirectoryHost);
        }

        _volumeMounts = mounts;
    }

    public static async Task<DockerSandbox> CreateAsync(SandboxOptions? options, CancellationToken cancellationToken = default)
    {
        // One sandbox instance == one long-lived container.
        // This keeps command execution fast (docker exec) and enables background jobs/log retrieval.
        var sandbox = new DockerSandbox(options, new DockerCliRunner());
        await sandbox.StartContainerAsync(cancellationToken);
        return sandbox;
    }

    // Internal overload for unit tests (allows injecting a fake docker runner).
    internal static async Task<DockerSandbox> CreateAsync(
        SandboxOptions? options,
        IDockerRunner dockerRunner,
        CancellationToken cancellationToken = default)
    {
        var sandbox = new DockerSandbox(options, dockerRunner);
        await sandbox.StartContainerAsync(cancellationToken);
        return sandbox;
    }

    public async Task<CommandResult> ExecuteCommandAsync(
        string command,
        CommandOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        EnsureStarted();

        // Important: even though commands run in Docker, we still enforce the same boundary checks for any specified
        // working directory (host-side), and map it to a corresponding container directory.
        var resolvedWorkDirHost = ResolveWorkingDirectoryHost(options?.WorkingDirectory);
        if (!IsInside(resolvedWorkDirHost))
        {
            return new CommandResult
            {
                ExitCode = 1,
                Stdout = "",
                Stderr = $"Working directory outside sandbox: {options?.WorkingDirectory}"
            };
        }

        var timeout = options?.Timeout ?? _defaultTimeout;
        var containerWorkDir = MapHostPathToContainerWorkingDir(resolvedWorkDirHost);

        // Merge environment variables: sandbox-level + per-command.
        var env = new Dictionary<string, string>(_environmentVariables, StringComparer.Ordinal);
        if (options?.EnvironmentVariables != null)
        {
            foreach (var (k, v) in options.EnvironmentVariables)
            {
                env[k] = v;
            }
        }

        if (options?.Background == true)
        {
            // Background execution model:
            // - Start the command in container using a wrapper that (a) returns its container PID, and
            //   (b) writes stdout/stderr/exit code into files under /workspace/.kode/...
            // - We track those host paths so bash_logs / bash_kill can work.
            var jobId = Interlocked.Increment(ref _nextJobId);
            var jobDirHost = Path.Combine(_stateDirectoryHost, "sandbox", SandboxId, "jobs", jobId.ToString());
            Directory.CreateDirectory(jobDirHost);

            var stdoutHost = Path.Combine(jobDirHost, "stdout.txt");
            var stderrHost = Path.Combine(jobDirHost, "stderr.txt");
            var exitHost = Path.Combine(jobDirHost, "exitcode.txt");

            var stdoutContainer = $"{_stateDirectoryContainerBase}/sandbox/{SandboxId}/jobs/{jobId}/stdout.txt";
            var stderrContainer = $"{_stateDirectoryContainerBase}/sandbox/{SandboxId}/jobs/{jobId}/stderr.txt";
            var exitContainer = $"{_stateDirectoryContainerBase}/sandbox/{SandboxId}/jobs/{jobId}/exitcode.txt";

            var pid = await StartBackgroundInContainerAsync(
                command,
                containerWorkDir,
                stdoutContainer,
                stderrContainer,
                exitContainer,
                env,
                cancellationToken);

            _jobs[jobId] = new BackgroundJob
            {
                JobId = jobId,
                Command = command,
                ContainerPid = pid,
                ContainerWorkingDirectory = containerWorkDir,
                StartedAt = DateTime.UtcNow,
                StdoutPath = stdoutHost,
                StderrPath = stderrHost,
                ExitCodePath = exitHost
            };

            return new CommandResult
            {
                ExitCode = 0,
                Stdout = "",
                Stderr = "",
                ProcessId = jobId
            };
        }

        // Foreground execution uses the same background wrapper:
        // - This gives us consistent stdout/stderr capture.
        // - We can enforce timeout by waiting for the exit-code file and killing the process group if needed.
        var fgJobId = Interlocked.Increment(ref _nextJobId);
        var fgDirHost = Path.Combine(_stateDirectoryHost, "sandbox", SandboxId, "jobs", fgJobId.ToString());
        Directory.CreateDirectory(fgDirHost);

        var fgStdoutHost = Path.Combine(fgDirHost, "stdout.txt");
        var fgStderrHost = Path.Combine(fgDirHost, "stderr.txt");
        var fgExitHost = Path.Combine(fgDirHost, "exitcode.txt");

        var fgStdoutContainer = $"{_stateDirectoryContainerBase}/sandbox/{SandboxId}/jobs/{fgJobId}/stdout.txt";
        var fgStderrContainer = $"{_stateDirectoryContainerBase}/sandbox/{SandboxId}/jobs/{fgJobId}/stderr.txt";
        var fgExitContainer = $"{_stateDirectoryContainerBase}/sandbox/{SandboxId}/jobs/{fgJobId}/exitcode.txt";

        var fgPid = await StartBackgroundInContainerAsync(
            command,
            containerWorkDir,
            fgStdoutContainer,
            fgStderrContainer,
            fgExitContainer,
            env,
            cancellationToken);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            // We consider the command finished when it writes an exit-code file.
            await WaitForExitFileAsync(fgExitHost, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout: best-effort kill the container process group to stop any spawned subprocesses.
            await KillContainerProcessGroupAsync(fgPid, CancellationToken.None);
            throw;
        }

        var exitCode = TryReadExitCodeFile(fgExitHost);
        var stdout = SafeReadAllText(fgStdoutHost);
        var stderr = SafeReadAllText(fgStderrHost);

        return new CommandResult
        {
            ExitCode = exitCode ?? 1,
            Stdout = stdout,
            Stderr = stderr
        };
    }

    public Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        return File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    public async Task WriteFileAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
    }

    public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        return Task.FromResult(Directory.Exists(fullPath));
    }

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        Directory.CreateDirectory(fullPath);
        return Task.CompletedTask;
    }

    public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string path, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GlobAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // Keep behavior consistent with LocalSandbox.GlobAsync (simple best-effort glob over _workingDirectory).
        var fileNamePattern = Path.GetFileName(pattern);
        if (string.IsNullOrEmpty(fileNamePattern) || fileNamePattern == "**" || fileNamePattern == ".")
        {
            fileNamePattern = "*";
        }

        var searchPattern = fileNamePattern.Replace("**", "*");
        var searchOption = pattern.Contains("**") ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        try
        {
            var files = Directory.GetFiles(_workingDirectory, searchPattern, searchOption)
                .Select(f => Path.GetRelativePath(_workingDirectory, f))
                .ToList();
            return Task.FromResult<IReadOnlyList<string>>(files);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }

    public async Task<IReadOnlyList<GrepResult>> GrepAsync(
        string pattern,
        GrepOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<GrepResult>();
        var filePattern = options?.FilePattern ?? "*";
        var isRegex = options?.IsRegex ?? false;
        var caseSensitive = options?.CaseSensitive ?? false;
        var maxResults = options?.MaxResults ?? 100;

        var files = await GlobAsync(filePattern, cancellationToken);

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        System.Text.RegularExpressions.Regex? regex = null;
        if (isRegex)
        {
            var regexOptions = caseSensitive
                ? System.Text.RegularExpressions.RegexOptions.None
                : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
            regex = new System.Text.RegularExpressions.Regex(pattern, regexOptions);
        }

        foreach (var file in files)
        {
            if (results.Count >= maxResults) break;

            var fullPath = GetFullPath(file);
            if (!File.Exists(fullPath)) continue;

            var lines = await File.ReadAllLinesAsync(fullPath, cancellationToken);
            for (var i = 0; i < lines.Length && results.Count < maxResults; i++)
            {
                var line = lines[i];
                var matches = isRegex
                    ? regex!.IsMatch(line)
                    : line.Contains(pattern, comparison);

                if (!matches) continue;

                var contextBefore = options?.ContextBefore ?? 0;
                var contextAfter = options?.ContextAfter ?? 0;

                results.Add(new GrepResult
                {
                    FilePath = file,
                    LineNumber = i + 1,
                    Line = line,
                    ContextBefore = contextBefore > 0
                        ? lines.Skip(Math.Max(0, i - contextBefore)).Take(Math.Min(contextBefore, i)).ToList()
                        : null,
                    ContextAfter = contextAfter > 0
                        ? lines.Skip(i + 1).Take(contextAfter).ToList()
                        : null
                });
            }
        }

        return results;
    }

    public Task<IReadOnlyList<DirectoryEntry>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        var results = new List<DirectoryEntry>();

        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult<IReadOnlyList<DirectoryEntry>>(results);
        }

        foreach (var dir in Directory.GetDirectories(fullPath))
        {
            var dirInfo = new DirectoryInfo(dir);
            results.Add(new DirectoryEntry
            {
                Name = dirInfo.Name,
                Path = Path.GetRelativePath(_workingDirectory, dir),
                IsDirectory = true,
                LastModified = dirInfo.LastWriteTimeUtc
            });
        }

        foreach (var file in Directory.GetFiles(fullPath))
        {
            var fileInfo = new FileInfo(file);
            results.Add(new DirectoryEntry
            {
                Name = fileInfo.Name,
                Path = Path.GetRelativePath(_workingDirectory, file),
                IsDirectory = false,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc
            });
        }

        return Task.FromResult<IReadOnlyList<DirectoryEntry>>(results);
    }

    public async Task<ProcessInfo?> GetProcessAsync(int processId, CancellationToken cancellationToken = default)
    {
        EnsureStarted();

        if (!_jobs.TryGetValue(processId, out var job))
        {
            return null;
        }

        // Primary completion signal is the exit-code file written by the wrapper.
        var exitCode = job.ExitCode ?? TryReadExitCodeFile(job.ExitCodePath);
        if (exitCode.HasValue && job.ExitCode != exitCode)
        {
            job.ExitCode = exitCode;
            job.EndedAt ??= DateTime.UtcNow;
        }

        // If exit code isn't available yet, fall back to checking if the container PID is still alive.
        var running = exitCode == null && await IsContainerPidRunningAsync(job.ContainerPid, cancellationToken);

        return new ProcessInfo
        {
            ProcessId = processId,
            Command = job.Command,
            IsRunning = running,
            ExitCode = exitCode,
            Stdout = SafeReadAllText(job.StdoutPath),
            Stderr = SafeReadAllText(job.StderrPath),
            StartedAt = job.StartedAt,
            EndedAt = job.EndedAt
        };
    }

    public async Task<bool> KillProcessAsync(int processId, CancellationToken cancellationToken = default)
    {
        EnsureStarted();

        if (!_jobs.TryGetValue(processId, out var job))
        {
            return false;
        }

        // Kill the entire process group (negative PID) first so child processes are also terminated.
        await KillContainerProcessGroupAsync(job.ContainerPid, cancellationToken);
        job.EndedAt ??= DateTime.UtcNow;
        job.ExitCode ??= TryReadExitCodeFile(job.ExitCodePath) ?? -1;
        return true;
    }

    public Task<IReadOnlyList<ProcessInfo>> ListProcessesAsync(CancellationToken cancellationToken = default)
    {
        return ListProcessesCoreAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ProcessInfo>> ListProcessesCoreAsync(CancellationToken cancellationToken)
    {
        var ids = _jobs.Keys.OrderBy(id => id).ToArray();
        var results = new List<ProcessInfo>(ids.Length);

        foreach (var id in ids)
        {
            var info = await GetProcessAsync(id, cancellationToken);
            if (info != null)
            {
                results.Add(info);
            }
        }

        return results;
    }

    public async ValueTask DisposeAsync()
    {
        // On sandbox disposal we remove the container and drop all job metadata.
        // This is best-effort cleanup; we intentionally ignore cleanup errors.
        if (_containerName != null)
        {
            try
            {
                await _dockerRunner.RunAsync(["rm", "-f", _containerName], _defaultTimeout, CancellationToken.None);
            }
            catch
            {
                // ignore cleanup errors
            }
        }

        _jobs.Clear();
    }

    private async Task StartContainerAsync(CancellationToken cancellationToken)
    {
        _containerName = $"kode-sandbox-{SandboxId}";

        // Container hardening (best-effort):
        // - --cap-drop=ALL + no-new-privileges: reduce privilege escalation surface
        // - --init: reap zombies when running background jobs
        // - --network: default is "none" (configurable)
        //
        // Note: we currently don't set --user / --read-only. Those can be added if you want stricter isolation.
        var args = new List<string>
        {
            "run",
            "-d",
            "--name",
            _containerName,
            "--init",
            "--cap-drop=ALL",
            "--security-opt=no-new-privileges",
            "--workdir",
            "/workspace"
        };

        if (!string.IsNullOrWhiteSpace(_dockerNetworkMode))
        {
            args.Add("--network");
            args.Add(_dockerNetworkMode);
        }

        foreach (var (host, container) in _volumeMounts)
        {
            args.Add("-v");
            args.Add($"{host}:{container}:rw");
        }

        args.Add(_dockerImage);
        args.Add("bash");
        args.Add("-lc");
        args.Add("sleep infinity");

        var result = await _dockerRunner.RunAsync(args, _defaultTimeout, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to start Docker sandbox container using image '{_dockerImage}'. " +
                $"Ensure Docker is running and the image is available locally.\n{result.Stderr}");
        }
    }

    private void EnsureStarted()
    {
        if (string.IsNullOrWhiteSpace(_containerName))
        {
            throw new InvalidOperationException("Docker sandbox container not started.");
        }
    }

    private string ResolveWorkingDirectoryHost(string? workDir)
    {
        if (string.IsNullOrWhiteSpace(workDir))
        {
            return _workingDirectory;
        }

        if (Path.IsPathRooted(workDir))
        {
            return Path.GetFullPath(workDir);
        }

        return Path.GetFullPath(Path.Combine(_workingDirectory, workDir));
    }

    private string GetFullPath(string path)
    {
        var resolved = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(_workingDirectory, path));

        if (!IsInside(resolved))
        {
            throw new UnauthorizedAccessException($"Path outside sandbox: {path}");
        }

        return resolved;
    }

    private bool IsInside(string fullPath)
    {
        // 1) Always allow anything under sandbox working directory
        var relativeToWork = Path.GetRelativePath(_workingDirectory, fullPath);
        if (!relativeToWork.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relativeToWork))
        {
            return true;
        }

        // 2) If boundary checks are disabled, allow all paths
        if (!_enforceBoundary)
        {
            return true;
        }

        // 3) Allow whitelist paths
        foreach (var allowed in _allowPaths)
        {
            var relative = Path.GetRelativePath(allowed, fullPath);
            if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
            {
                return true;
            }
        }

        return false;
    }

    private string MapHostPathToContainerWorkingDir(string hostPath)
    {
        // Inside working directory => /workspace/<rel>
        if (IsUnder(_workingDirectory, hostPath))
        {
            var rel = Path.GetRelativePath(_workingDirectory, hostPath);
            return string.IsNullOrWhiteSpace(rel) || rel == "."
                ? "/workspace"
                : $"/workspace/{NormalizeContainerPath(rel)}";
        }

        // Inside allow path => /mnt/allowX/<rel>
        foreach (var (host, container) in _volumeMounts)
        {
            if (host == _workingDirectory) continue;
            if (IsUnder(host, hostPath))
            {
                var rel = Path.GetRelativePath(host, hostPath);
                return string.IsNullOrWhiteSpace(rel) || rel == "."
                    ? container
                    : $"{container}/{NormalizeContainerPath(rel)}";
            }
        }

        // Fallback: keep /workspace (we already checked boundary; this should be unreachable)
        return "/workspace";
    }

    private async Task<int> StartBackgroundInContainerAsync(
        string command,
        string containerWorkDir,
        string stdoutContainer,
        string stderrContainer,
        string exitContainer,
        Dictionary<string, string> env,
        CancellationToken cancellationToken)
    {
        EnsureStarted();

        // Wrapper details:
        // - `setsid` makes the spawned bash process a process-group leader, enabling "kill -TERM -<pid>".
        // - stdout/stderr redirect to deterministic files under /workspace (host-mounted), so logs survive `docker exec`.
        // - exit code is written to a file, so we can detect completion without holding an open process handle.
        // - `echo $!` prints the container PID of the spawned background process.
        var script =
            $"mkdir -p \"$(dirname '{EscapeForBashSingleQuotes(stdoutContainer)}')\" && " +
            $"cd '{EscapeForBashSingleQuotes(containerWorkDir)}' && " +
            $"(setsid bash -lc '{EscapeForBashSingleQuotes(command)}; echo $? > \"{EscapeForBashDoubleQuotes(exitContainer)}\"' " +
            $"> \"{EscapeForBashDoubleQuotes(stdoutContainer)}\" 2> \"{EscapeForBashDoubleQuotes(stderrContainer)}\" < /dev/null & echo $!)";

        // We invoke `docker` with ProcessStartInfo.ArgumentList to avoid shell-quoting injection on the host.
        var args = new List<string> { "exec" };
        foreach (var (k, v) in env)
        {
            args.Add("-e");
            args.Add($"{k}={v}");
        }
        args.Add(_containerName!);
        args.Add("bash");
        args.Add("-lc");
        args.Add(script);

        var result = await _dockerRunner.RunAsync(args, _defaultTimeout, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to start background command in container: {result.Stderr}");
        }

        if (!int.TryParse(result.Stdout.Trim(), out var pid) || pid <= 0)
        {
            throw new InvalidOperationException($"Failed to parse container PID from docker exec output: '{result.Stdout}'");
        }

        return pid;
    }

    private async Task<bool> IsContainerPidRunningAsync(int pid, CancellationToken cancellationToken)
    {
        // "kill -0" returns 0 if the process exists and we have permission.
        var args = new List<string>
        {
            "exec",
            _containerName!,
            "bash",
            "-lc",
            $"kill -0 {pid} 2>/dev/null"
        };

        var result = await _dockerRunner.RunAsync(args, _defaultTimeout, cancellationToken);
        return result.ExitCode == 0;
    }

    private async Task KillContainerProcessGroupAsync(int pid, CancellationToken cancellationToken)
    {
        // Best effort: kill process group first, then the PID itself.
        var args = new List<string>
        {
            "exec",
            _containerName!,
            "bash",
            "-lc",
            $"kill -TERM -{pid} 2>/dev/null || kill -TERM {pid} 2>/dev/null || true"
        };

        await _dockerRunner.RunAsync(args, _defaultTimeout, cancellationToken);
    }

    private static async Task WaitForExitFileAsync(string exitCodeFile, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (File.Exists(exitCodeFile))
            {
                return;
            }
            await Task.Delay(200, cancellationToken);
        }
    }

    private static int? TryReadExitCodeFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var txt = File.ReadAllText(path).Trim();
            return int.TryParse(txt, out var code) ? code : null;
        }
        catch
        {
            return null;
        }
    }

    private static string SafeReadAllText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
        catch
        {
            return "";
        }
    }

    private static bool IsUnder(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath);
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    private static string NormalizeContainerPath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }

    private static IEqualityComparer<string> GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    // Escape for embedding inside single quotes in bash: abc'def => abc'"'"'def
    private static string EscapeForBashSingleQuotes(string value)
    {
        return value.Replace("'", "'\"'\"'");
    }

    // Escape for embedding inside double quotes in bash: escape backslash and double quotes.
    private static string EscapeForBashDoubleQuotes(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    // Note: actual docker invocation is delegated to IDockerRunner (DockerCliRunner by default).
}

/// <summary>
/// Factory for creating Docker sandbox instances.
/// </summary>
public sealed class DockerSandboxFactory : ISandboxFactory
{
    public async Task<ISandbox> CreateAsync(SandboxOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await DockerSandbox.CreateAsync(options, cancellationToken);
    }
}
