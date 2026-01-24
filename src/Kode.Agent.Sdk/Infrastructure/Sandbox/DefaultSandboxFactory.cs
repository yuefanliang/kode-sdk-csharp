using Kode.Agent.Sdk.Core.Abstractions;

namespace Kode.Agent.Sdk.Infrastructure.Sandbox;

/// <summary>
/// Default sandbox factory.
/// Selects DockerSandbox when SandboxOptions.UseDocker is enabled; otherwise uses LocalSandbox.
///
/// This keeps the decision close to configuration (AgentConfig.SandboxOptions),
/// so callers don't need to swap DI registrations to toggle Docker isolation.
/// </summary>
public sealed class DefaultSandboxFactory : ISandboxFactory
{
    private readonly ISandboxFactory _local = new LocalSandboxFactory();
    private readonly ISandboxFactory _docker = new DockerSandboxFactory();

    public Task<ISandbox> CreateAsync(SandboxOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (options?.UseDocker == true)
        {
            return _docker.CreateAsync(options, cancellationToken);
        }

        return _local.CreateAsync(options, cancellationToken);
    }
}
