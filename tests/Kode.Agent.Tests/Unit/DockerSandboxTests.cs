using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Infrastructure.Sandbox;
using Xunit;

namespace Kode.Agent.Tests.Unit;

public class DockerSandboxTests
{
    [Fact]
    public async Task CreateAsync_ConstructsExpectedDockerRunArgs()
    {
        using var workDir = new TempDir();
        using var allowDir = new TempDir();

        var runner = new RecordingDockerRunner();
        var options = new SandboxOptions
        {
            WorkingDirectory = workDir.Path,
            EnforceBoundary = true,
            AllowPaths = new[] { allowDir.Path },
            UseDocker = true,
            DockerImage = "ubuntu:latest",
            DockerNetworkMode = "none"
        };

        await using var sandbox = await DockerSandbox.CreateAsync(options, runner);

        Assert.NotEmpty(runner.Calls);

        var runCall = runner.Calls[0];
        Assert.True(runCall.Count > 0);
        Assert.Equal("run", runCall[0]);

        Assert.Contains("--network", runCall);
        var networkIndex = IndexOf(runCall, "--network");
        Assert.True(networkIndex >= 0 && networkIndex + 1 < runCall.Count);
        Assert.Equal("none", runCall[networkIndex + 1]);

        Assert.Contains("-v", runCall);
        Assert.Contains($"{workDir.Path}:/workspace:rw", runCall);
        Assert.Contains($"{allowDir.Path}:/mnt/allow0:rw", runCall);

        Assert.Contains("ubuntu:latest", runCall);
    }

    [Fact]
    public async Task ExecuteCommandAsync_WithAllowPathWorkingDirectory_MapsToMountedContainerPath()
    {
        using var workDir = new TempDir();
        using var allowDir = new TempDir();

        var runner = new RecordingDockerRunner();
        var options = new SandboxOptions
        {
            WorkingDirectory = workDir.Path,
            EnforceBoundary = true,
            AllowPaths = new[] { allowDir.Path },
            UseDocker = true,
            DockerImage = "ubuntu:latest",
            DockerNetworkMode = "none"
        };

        await using var sandbox = await DockerSandbox.CreateAsync(options, runner);

        var result = await sandbox.ExecuteCommandAsync("echo hi", new CommandOptions
        {
            Background = true,
            WorkingDirectory = allowDir.Path
        });

        Assert.True(result.Success);
        Assert.True(result.ProcessId.HasValue);

        // Find the docker exec call that starts the background command.
        var execCall = runner.Calls.FirstOrDefault(c => c.Count > 0 && c[0] == "exec");
        Assert.NotNull(execCall);

        // Script is passed as the last argument to: docker exec ... bash -lc "<script>"
        var script = execCall![^1];
        Assert.Contains("cd '/mnt/allow0'", script);
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value) return i;
        }
        return -1;
    }

    private sealed class RecordingDockerRunner : IDockerRunner
    {
        public List<IReadOnlyList<string>> Calls { get; } = new();

        public Task<DockerRunResult> RunAsync(
            IReadOnlyList<string> args,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Calls.Add(args.ToArray());

            return Task.FromResult(args.Count == 0 ? new DockerRunResult(1, "", "empty args") : args[0] switch
            {
                // docker run -d ...
                "run" => new DockerRunResult(0, "container-id\n", ""),

                // docker exec ... (we return a fake PID for "echo $!" parsing)
                "exec" => new DockerRunResult(0, "123\n", ""),

                // docker rm -f ...
                "rm" => new DockerRunResult(0, "", ""),

                _ => new DockerRunResult(0, "", "")
            });
        }
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kode-agent-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }
}
