using System.Diagnostics;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Infrastructure.Sandbox;
using Kode.Agent.Tests.Helpers;
using Xunit;

namespace Kode.Agent.Tests.Integration;

public sealed class DockerSandboxIntegrationTests
{
    [DockerIntegrationFact]
    public async Task DockerSandbox_ExecuteCommandAsync_RunsCommandAndCapturesOutput()
    {
        var image = Environment.GetEnvironmentVariable("KODE_TEST_DOCKER_IMAGE") ?? "ubuntu:latest";

        using var workDir = new TempDir();

        await using var sandbox = await DockerSandbox.CreateAsync(new SandboxOptions
        {
            WorkingDirectory = workDir.Path,
            EnforceBoundary = true,
            UseDocker = true,
            DockerImage = image,
            DockerNetworkMode = "none",
            Timeout = TimeSpan.FromSeconds(30)
        });

        var result = await sandbox.ExecuteCommandAsync("echo hello-from-docker");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello-from-docker", result.Stdout);
    }

    [DockerIntegrationFact]
    public async Task DockerSandbox_BackgroundCommand_CanReadLogsAndKill()
    {
        var image = Environment.GetEnvironmentVariable("KODE_TEST_DOCKER_IMAGE") ?? "ubuntu:latest";

        using var workDir = new TempDir();

        await using var sandbox = await DockerSandbox.CreateAsync(new SandboxOptions
        {
            WorkingDirectory = workDir.Path,
            EnforceBoundary = true,
            UseDocker = true,
            DockerImage = image,
            DockerNetworkMode = "none",
            Timeout = TimeSpan.FromSeconds(30)
        });

        var bg = await sandbox.ExecuteCommandAsync(
            "echo start; sleep 10; echo end",
            new CommandOptions { Background = true });

        Assert.True(bg.Success);
        Assert.True(bg.ProcessId.HasValue);

        // Wait until we see the initial output.
        var started = await WaitUntilAsync(async () =>
        {
            var info = await sandbox.GetProcessAsync(bg.ProcessId!.Value);
            return info != null && (info.Stdout?.Contains("start") == true);
        }, timeout: TimeSpan.FromSeconds(5));

        Assert.True(started);

        // Then kill the process and confirm it is no longer running.
        var killed = await sandbox.KillProcessAsync(bg.ProcessId!.Value);
        Assert.True(killed);

        var stopped = await WaitUntilAsync(async () =>
        {
            var info = await sandbox.GetProcessAsync(bg.ProcessId!.Value);
            return info != null && info.IsRunning == false;
        }, timeout: TimeSpan.FromSeconds(5));

        Assert.True(stopped);
    }

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> predicate, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            if (await predicate())
            {
                return true;
            }

            await Task.Delay(200);
        }

        return false;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kode-agent-integration-{Guid.NewGuid():N}");
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
