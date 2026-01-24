using System.Diagnostics;

namespace Kode.Agent.Sdk.Infrastructure.Sandbox;

/// <summary>
/// Abstraction over "docker" command execution.
/// This exists mainly to make DockerSandbox unit-testable (so tests can validate argument construction
/// without requiring Docker to be installed/running).
/// </summary>
internal interface IDockerRunner
{
    Task<DockerRunResult> RunAsync(
        IReadOnlyList<string> args,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

internal readonly record struct DockerRunResult(int ExitCode, string Stdout, string Stderr);

/// <summary>
/// Default implementation that executes the local "docker" CLI.
/// </summary>
internal sealed class DockerCliRunner : IDockerRunner
{
    public async Task<DockerRunResult> RunAsync(
        IReadOnlyList<string> args,
        TimeSpan timeout,
        CancellationToken cancellationToken)
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
        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }
            throw;
        }

        return new DockerRunResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}

