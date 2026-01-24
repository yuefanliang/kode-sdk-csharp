using Microsoft.Extensions.Hosting;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// Optional background warmup for MCP connections (and tool listing) at startup.
/// </summary>
public sealed class McpWarmupHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly McpServersLoader _loader;
    private readonly ILogger<McpWarmupHostedService> _logger;

    public McpWarmupHostedService(
        IConfiguration configuration,
        IHostApplicationLifetime lifetime,
        McpServersLoader loader,
        ILogger<McpWarmupHostedService> logger)
    {
        _configuration = configuration;
        _lifetime = lifetime;
        _loader = loader;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var enabled = _configuration.GetValue<bool?>("McpWarmup:Enabled") ?? false;
        if (!enabled)
        {
            _logger.LogInformation("MCP warmup disabled (McpWarmup:Enabled=false)");
            return Task.CompletedTask;
        }

        var preloadTools = _configuration.GetValue<bool?>("McpWarmup:PreloadTools") ?? false;

        // Fire-and-forget: don't block the host from starting.
        _ = Task.Run(
            async () =>
            {
                try
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        _lifetime.ApplicationStopping);

                    await _loader.WarmupAsync(_configuration, preloadTools, linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown.
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "MCP warmup failed unexpectedly");
                }
            },
            cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

