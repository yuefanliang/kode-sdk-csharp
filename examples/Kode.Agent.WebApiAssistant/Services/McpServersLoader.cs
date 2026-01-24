using System.Collections.Concurrent;
using Kode.Agent.Mcp;
using Kode.Agent.Sdk.Core.Abstractions;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// MCP 服务器配置（从 appsettings.json 读取）
/// </summary>
public class McpServerConfigOptions
{
    public string? Command { get; set; }
    public string[]? Args { get; set; }
    public string? Type { get; set; }
    public string? Transport { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public Dictionary<string, string>? Env { get; set; }
}

/// <summary>
/// MCP 服务器加载服务
/// 从 appsettings.json 的 McpServers 配置中加载 MCP 工具
/// </summary>
public sealed class McpServersLoader : IAsyncDisposable
{
    private readonly McpClientManager _mcpManager;
    private readonly ILogger<McpServersLoader> _logger;
    private readonly ConcurrentDictionary<string, McpConfig> _serverConfigs = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<ITool>>>> _toolsCache = new();
    private bool _disposed;
    private int _configsLoaded;

    public McpServersLoader(
        McpClientManager mcpManager,
        ILogger<McpServersLoader> logger)
    {
        _mcpManager = mcpManager;
        _logger = logger;
    }

    /// <summary>
    /// Pre-warms MCP server connections on startup (optional).
    /// </summary>
    public async Task WarmupAsync(
        IConfiguration configuration,
        bool preloadTools = false,
        CancellationToken cancellationToken = default)
    {
        LoadServerConfigs(configuration);

        if (_serverConfigs.Count == 0)
        {
            _logger.LogInformation("MCP warmup skipped: no servers configured");
            return;
        }

        _logger.LogInformation(
            "MCP warmup started. Servers={Count} PreloadTools={PreloadTools}",
            _serverConfigs.Count,
            preloadTools);

        foreach (var (serverName, mcpConfig) in _serverConfigs)
        {
            try
            {
                if (preloadTools)
                {
                    _ = await GetToolsForServerAsync(serverName, mcpConfig, cancellationToken);
                }
                else
                {
                    _ = await _mcpManager.ConnectAsync(serverName, mcpConfig, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MCP warmup failed for server: {ServerName}", serverName);
            }
        }

        _logger.LogInformation("MCP warmup completed. ConnectedServers={ConnectedCount}", _mcpManager.ConnectedServers.Count);
    }

    /// <summary>
    /// 从配置加载 MCP 服务器并注册工具
    /// </summary>
    /// <returns>加载的 MCP 工具名列表</returns>
    public async Task<IReadOnlyList<string>> LoadAndRegisterServersAsync(
        IConfiguration configuration,
        IToolRegistry toolRegistry,
        CancellationToken cancellationToken = default)
    {
        LoadServerConfigs(configuration);
        if (_serverConfigs.Count == 0)
        {
            _logger.LogInformation("No MCP servers configuration found");
            return Array.Empty<string>();
        }

        _logger.LogInformation("Found {Count} MCP servers in configuration", _serverConfigs.Count);

        var mcpToolNames = new List<string>();
        foreach (var (serverName, mcpConfig) in _serverConfigs)
        {
            try
            {
                // Load tools from this server (McpToolProvider logs the tool count)
                var tools = await GetToolsForServerAsync(serverName, mcpConfig, cancellationToken);

                // Register each tool and collect names
                foreach (var tool in tools)
                {
                    toolRegistry.Register(tool);
                    mcpToolNames.Add(tool.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load MCP server {ServerName}", serverName);
            }
        }

        _logger.LogInformation("Total MCP tools loaded: {TotalCount}", mcpToolNames.Count);
        return mcpToolNames;
    }

    private Task<IReadOnlyList<ITool>> GetToolsForServerAsync(
        string serverName,
        McpConfig mcpConfig,
        CancellationToken cancellationToken)
    {
        var lazy = _toolsCache.GetOrAdd(
            serverName,
            _ => new Lazy<Task<IReadOnlyList<ITool>>>(() =>
                McpToolProvider.GetToolsAsync(_mcpManager, mcpConfig, _logger, cancellationToken)));

        return lazy.Value;
    }

    private void LoadServerConfigs(IConfiguration configuration)
    {
        if (Interlocked.Exchange(ref _configsLoaded, 1) == 1)
        {
            return;
        }

        var mcpServersSection = configuration.GetSection("McpServers");
        if (!mcpServersSection.Exists())
        {
            return;
        }

        var mcpServers = mcpServersSection.GetChildren().ToList();
        foreach (var serverSection in mcpServers)
        {
            var serverName = serverSection.Key;
            var serverConfig = serverSection.Get<McpServerConfigOptions>();

            if (serverConfig == null)
            {
                _logger.LogWarning("Skipping MCP server {ServerName}: invalid configuration", serverName);
                continue;
            }

            var mcpConfig = ConvertToMcpConfig(serverName, serverConfig);
            if (mcpConfig == null)
            {
                _logger.LogWarning("Skipping MCP server {ServerName}: unsupported/invalid configuration", serverName);
                continue;
            }

            _serverConfigs.TryAdd(serverName, mcpConfig);
        }
    }

    /// <summary>
    /// 转换配置为 McpConfig
    /// </summary>
    private static McpConfig? ConvertToMcpConfig(string serverName, McpServerConfigOptions config)
    {
        // 确定传输类型
        var transportRaw = config.Transport ?? config.Type ?? "";
        var transport = transportRaw.ToLowerInvariant() switch
        {
            "stdio" => McpTransportType.Stdio,
            "http" => McpTransportType.Http,
            "streamablehttp" => McpTransportType.StreamableHttp,
            "sse" => McpTransportType.Sse,
            _ => McpTransportType.Stdio // 默认 stdio
        };

        // Stdio transport
        if (transport == McpTransportType.Stdio)
        {
            if (string.IsNullOrEmpty(config.Command))
            {
                return null;
            }

            return new McpConfig
            {
                ServerName = serverName,
                Transport = McpTransportType.Stdio,
                Command = config.Command,
                Args = config.Args,
                Environment = config.Env
            };
        }

        // HTTP/SSE transport
        if (transport is McpTransportType.Http or McpTransportType.StreamableHttp or McpTransportType.Sse)
        {
            if (string.IsNullOrEmpty(config.Url))
            {
                return null;
            }

            return new McpConfig
            {
                ServerName = serverName,
                Transport = transport,
                Url = config.Url,
                Headers = config.Headers
            };
        }

        return null;
    }

    /// <summary>
    /// 断开所有 MCP 服务器连接
    /// </summary>
    public async Task DisconnectAllAsync()
    {
        await _mcpManager.DisconnectAllAsync();
    }

    /// <summary>
    /// 获取已加载的服务器列表
    /// </summary>
    public IReadOnlyList<string> LoadedServers => _serverConfigs.Keys.ToList();

    /// <summary>
    /// 释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            // Best-effort cleanup: do not crash host shutdown if an MCP stdio process already exited.
            await DisconnectAllAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "McpServersLoader.DisconnectAllAsync failed during disposal (ignored)");
        }

        try
        {
            await _mcpManager.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "McpClientManager.DisposeAsync failed during disposal (ignored)");
        }
    }
}
