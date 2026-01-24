using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;

namespace Kode.Agent.Tests.Helpers;

/// <summary>
/// Integration test fact that only runs when:
/// - Platform is Unix-like (Linux/macOS)
/// - Docker CLI is available and Docker daemon is running
/// - The configured image exists locally (no network pull)
/// </summary>
public sealed class DockerIntegrationFactAttribute : FactAttribute
{
    private static readonly object Gate = new();
    private static string? CachedSkipReason;

    public DockerIntegrationFactAttribute()
    {
        Skip = GetSkipReason();
    }

    private static string? GetSkipReason()
    {
        lock (Gate)
        {
            if (CachedSkipReason != null)
            {
                // Non-null means we previously determined it should be skipped.
                return CachedSkipReason;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CachedSkipReason = "Docker sandbox integration tests only run on Unix-like platforms (Linux, macOS).";
                return CachedSkipReason;
            }

            // Ensure docker CLI + daemon are available.
            if (!RunDockerOk(["version"], timeout: TimeSpan.FromSeconds(3)))
            {
                CachedSkipReason = "Docker is not available (docker CLI missing or daemon not running).";
                return CachedSkipReason;
            }

            // Ensure image exists locally (no pulling during tests).
            var image = Environment.GetEnvironmentVariable("KODE_TEST_DOCKER_IMAGE") ?? "ubuntu:latest";
            if (!RunDockerOk(["image", "inspect", image], timeout: TimeSpan.FromSeconds(3)))
            {
                CachedSkipReason =
                    $"Docker image '{image}' not found locally. " +
                    "Set KODE_TEST_DOCKER_IMAGE to an image that exists locally (and contains bash).";
                return CachedSkipReason;
            }

            // If all checks pass, return null => do not skip.
            return null;
        }
    }

    private static bool RunDockerOk(IReadOnlyList<string> args, TimeSpan timeout)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var a in args)
            {
                startInfo.ArgumentList.Add(a);
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

