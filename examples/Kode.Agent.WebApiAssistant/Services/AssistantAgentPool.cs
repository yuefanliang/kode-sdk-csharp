using System.Collections.Concurrent;
using AgentImpl = Kode.Agent.Sdk.Core.Agent.Agent;
using Kode.Agent.Sdk.Core.Types;
using Kode.Agent.WebApiAssistant;
using Kode.Agent.WebApiAssistant.Assistant;
using Kode.Agent.Sdk.Core.Abstractions;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// Caches assistant agents (and therefore their sandboxes) by agent/session id.
/// This enables "scheme B": one sandbox per session, reused across requests, with idle/LRU eviction.
/// </summary>
public sealed class AssistantAgentPool
{
    private sealed class Entry
    {
        public required AgentImpl Agent { get; init; }
        public DateTime LastAccessUtc;
        public int ActiveLeases;
    }

    public sealed class Lease : IAsyncDisposable
    {
        private readonly AssistantAgentPool _pool;
        private readonly string _agentId;
        private bool _disposed;

        public AgentImpl Agent { get; }
        public string AgentId => _agentId;
        public bool IsPooled { get; }

        internal Lease(AssistantAgentPool pool, string agentId, AgentImpl agent, bool pooled)
        {
            _pool = pool;
            _agentId = agentId;
            Agent = agent;
            IsPooled = pooled;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            if (IsPooled)
            {
                _pool.Release(_agentId);
                return;
            }

            await Agent.DisposeAsync();
        }
    }

    private readonly ConcurrentDictionary<string, Entry> _agents = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AgentDependencies _globalDeps;
    private readonly AssistantOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AssistantAgentPool> _logger;

    public AssistantAgentPool(
        AgentDependencies globalDeps,
        AssistantOptions options,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory)
    {
        _globalDeps = globalDeps;
        _options = options;
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AssistantAgentPool>();

        // _logger.LogInformation(
        //     "AssistantAgentPool initialized. Enabled={Enabled} UseDocker={UseDocker} PerAgentDataDir={PerAgentDataDir} MaxAgents={MaxAgents} IdleTimeout={IdleTimeout} SweepInterval={SweepInterval}",
        //     _options.UseAgentPool,
        //     _options.UseDockerSandbox,
        //     _options.UsePerAgentDataDir,
        //     _options.AgentPoolMaxAgents,
        //     _options.AgentPoolIdleTimeout,
        //     _options.AgentPoolSweepInterval);
    }

    public int Count => _agents.Count;

    public IReadOnlyList<string> ListAgentIds()
    {
        return _agents.Keys.ToArray();
    }

    public bool TryGetStatus(
        string agentId,
        out AgentRuntimeState runtimeState,
        out BreakpointState breakpointState,
        out DateTime lastAccessUtc,
        out int activeLeases)
    {
        if (_agents.TryGetValue(agentId, out var entry))
        {
            runtimeState = entry.Agent.RuntimeState;
            breakpointState = entry.Agent.BreakpointState;
            lastAccessUtc = entry.LastAccessUtc;
            activeLeases = entry.ActiveLeases;
            return true;
        }

        runtimeState = AgentRuntimeState.Ready;
        breakpointState = BreakpointState.Ready;
        lastAccessUtc = DateTime.MinValue;
        activeLeases = 0;
        return false;
    }

    public async Task<Lease> LeaseAsync(
        string agentId,
        GetOwnedAgentOptions opts,
        CancellationToken cancellationToken)
    {
        // If pooling is disabled, always create a fresh agent and dispose it at request end.
        if (!_options.UseAgentPool)
        {
            var agent = await CreateOrResumeAgentAsync(agentId, opts, cancellationToken);
            return new Lease(this, agentId, agent, pooled: false);
        }

        if (_agents.TryGetValue(agentId, out var existing))
        {
            Touch(existing);
            Interlocked.Increment(ref existing.ActiveLeases);
            _logger.LogInformation(
                "Agent lease acquired (reuse): {AgentId}. ActiveLeases={ActiveLeases} TotalAgents={TotalAgents}",
                agentId,
                existing.ActiveLeases,
                _agents.Count);
            return new Lease(this, agentId, existing.Agent, pooled: true);
        }

        await _gate.WaitAsync(cancellationToken);
        List<(string Id, AgentImpl Agent)>? evicted = null;
        try
        {
            if (_agents.TryGetValue(agentId, out existing))
            {
                Touch(existing);
                Interlocked.Increment(ref existing.ActiveLeases);
                _logger.LogInformation(
                    "Agent lease acquired (reuse): {AgentId}. ActiveLeases={ActiveLeases} TotalAgents={TotalAgents}",
                    agentId,
                    existing.ActiveLeases,
                    _agents.Count);
                return new Lease(this, agentId, existing.Agent, pooled: true);
            }

            // IMPORTANT: avoid deadlock.
            // We already hold _gate here; calling EvictAsync would try to take _gate again.
            // So we do a "locked eviction" that only mutates the dictionary and returns disposables,
            // then dispose them outside the lock.
            evicted = CollectEvictionsLocked();

            var created = await CreateOrResumeAgentAsync(agentId, opts, cancellationToken);
            var entry = new Entry
            {
                Agent = created,
                LastAccessUtc = DateTime.UtcNow,
                ActiveLeases = 1
            };
            _agents[agentId] = entry;
            _logger.LogInformation(
                "Agent lease acquired (create): {AgentId}. ActiveLeases={ActiveLeases} TotalAgents={TotalAgents}",
                agentId,
                entry.ActiveLeases,
                _agents.Count);
            return new Lease(this, agentId, created, pooled: true);
        }
        finally
        {
            _gate.Release();
            if (evicted is { Count: > 0 })
            {
                // Fire-and-forget disposal so the request path doesn't block on cleanup.
                // Disposal is best-effort and errors are logged.
                _ = DisposeEvictedAsync(evicted);
            }
        }
    }

    private void Touch(Entry entry)
    {
        entry.LastAccessUtc = DateTime.UtcNow;
    }

    private void Release(string agentId)
    {
        if (!_agents.TryGetValue(agentId, out var entry))
        {
            return;
        }

        Touch(entry);
        Interlocked.Decrement(ref entry.ActiveLeases);
    }

    public async Task<int> EvictAsync(CancellationToken cancellationToken)
    {
        if (!_options.UseAgentPool) return 0;

        List<(string Id, AgentImpl Agent)> evicted;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            evicted = CollectEvictionsLocked();
        }
        finally
        {
            _gate.Release();
        }

        // Dispose outside the lock.
        await DisposeEvictedAsync(evicted);

        return evicted.Count;
    }

    /// <summary>
    /// Disposes all cached agents immediately.
    /// This is intended for application shutdown (best-effort cleanup of Docker containers / background processes).
    /// </summary>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.UseAgentPool) return;

        List<(string Id, AgentImpl Agent)> evicted;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            evicted = _agents.Select(kvp => (kvp.Key, kvp.Value.Agent)).ToList();
            _agents.Clear();
        }
        finally
        {
            _gate.Release();
        }

        await DisposeEvictedAsync(evicted);
        _logger.LogInformation("AssistantAgentPool shutdown cleanup completed. Evicted={Count}", evicted.Count);
    }

    private List<(string Id, AgentImpl Agent)> CollectEvictionsLocked()
    {
        var evicted = new List<(string Id, AgentImpl Agent)>();
        var now = DateTime.UtcNow;
        var idle = _options.AgentPoolIdleTimeout;

        // 1) Evict idle agents (only if not in use)
        foreach (var (id, entry) in _agents)
        {
            if (entry.ActiveLeases > 0) continue;
            if (now - entry.LastAccessUtc < idle) continue;

            if (_agents.TryRemove(id, out var removed))
            {
                evicted.Add((id, removed.Agent));
            }
        }

        // 2) If still over capacity, LRU-evict more (only not in use)
        var max = _options.AgentPoolMaxAgents;
        if (_agents.Count > max)
        {
            var candidates = _agents
                .Where(kvp => kvp.Value.ActiveLeases == 0)
                .OrderBy(kvp => kvp.Value.LastAccessUtc)
                .ToList();

            foreach (var kvp in candidates)
            {
                if (_agents.Count <= max) break;
                if (_agents.TryRemove(kvp.Key, out var removed))
                {
                    evicted.Add((kvp.Key, removed.Agent));
                }
            }
        }

        return evicted;
    }

    private async Task DisposeEvictedAsync(IReadOnlyList<(string Id, AgentImpl Agent)> evicted)
    {
        foreach (var (id, agent) in evicted)
        {
            try
            {
                await agent.DisposeAsync();
                _logger.LogInformation("Agent evicted: {AgentId}. TotalAgents={Count}", id, _agents.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose evicted agent {AgentId}", id);
            }
        }
    }

    private async Task<AgentImpl> CreateOrResumeAgentAsync(
        string agentId,
        GetOwnedAgentOptions opts,
        CancellationToken cancellationToken)
    {
        var userDataDir = _options.UsePerAgentDataDir
            ? Path.Combine(_options.WorkDir, "data", agentId)
            : null;

        var createOptions = new CreateAssistantOptions
        {
            AgentId = agentId,
            WorkDir = _options.WorkDir,
            StoreDir = _options.StoreDir,
            UserDataDir = userDataDir,
            Model = _options.DefaultModel,
            SystemPrompt = opts.SystemPrompt ?? _options.DefaultSystemPrompt,
            Temperature = opts.Temperature,
            MaxTokens = opts.MaxTokens,
            Skills = _options.SkillsConfig,
            Permissions = _options.PermissionConfig,
            UseDockerSandbox = _options.UseDockerSandbox,
            DockerImage = _options.DockerImage,
            DockerNetworkMode = _options.DockerNetworkMode
        };

        return await AssistantBuilder.CreateAssistantAsync(
            createOptions,
            _globalDeps,
            _serviceProvider,
            _loggerFactory,
            cancellationToken);
    }
}
