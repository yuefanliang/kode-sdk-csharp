using Microsoft.Extensions.Hosting;
using Kode.Agent.WebApiAssistant;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// Periodically evicts idle agents from AssistantAgentPool.
/// </summary>
public sealed class AssistantAgentPoolEvictionService : BackgroundService
{
    private readonly AssistantAgentPool _pool;
    private readonly AssistantOptions _options;
    private readonly ILogger<AssistantAgentPoolEvictionService> _logger;

    public AssistantAgentPoolEvictionService(
        AssistantAgentPool pool,
        AssistantOptions options,
        ILogger<AssistantAgentPoolEvictionService> logger)
    {
        _pool = pool;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.UseAgentPool)
        {
            _logger.LogInformation("AssistantAgentPool disabled; eviction service idle.");
            return;
        }

        var interval = _options.AgentPoolSweepInterval;
        _logger.LogInformation(
            "AssistantAgentPool eviction started. MaxAgents={Max} IdleTimeout={Idle} Sweep={Sweep}",
            _options.AgentPoolMaxAgents,
            _options.AgentPoolIdleTimeout,
            interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var evicted = await _pool.EvictAsync(stoppingToken);
                if (evicted > 0)
                {
                    _logger.LogInformation("AssistantAgentPool evicted {Count} agents", evicted);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AssistantAgentPool eviction loop error");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        // Best-effort cleanup on shutdown: dispose all cached agents so their sandboxes/containers are removed.
        try
        {
            await _pool.ShutdownAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AssistantAgentPool shutdown cleanup failed");
        }
    }
}
