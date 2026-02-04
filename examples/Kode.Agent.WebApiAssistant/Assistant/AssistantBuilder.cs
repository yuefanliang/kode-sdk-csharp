using AgentImpl = Kode.Agent.Sdk.Core.Agent.Agent;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Types;
using Kode.Agent.Tools.Builtin;
using Kode.Agent.WebApiAssistant.Assistant.Hooks;
using Kode.Agent.WebApiAssistant.Extensions;
using Microsoft.Extensions.Logging;

namespace Kode.Agent.WebApiAssistant.Assistant;

/// <summary>
/// Result of building assistant hooks, includes hooks and MCP tool names.
/// </summary>
public record BuildAssistantHooksResult
{
    /// <summary>
    /// The composed hooks instance.
    /// </summary>
    public required Kode.Agent.Sdk.Core.Hooks.IHooks Hooks { get; init; }

    /// <summary>
    /// List of MCP tool names (namespaced as mcp__{server}__{tool}).
    /// </summary>
    public required IReadOnlyList<string> McpToolNames { get; init; }
}

/// <summary>
/// Options for building assistant hooks.
/// </summary>
public record BuildAssistantHooksOptions
{
    /// <summary>
    /// Agent sandbox dataDir (user data directory). Used in preModel phase to read/write profile.
    /// </summary>
    public string? DataDir { get; init; }
}

/// <summary>
/// Builder for creating Personal Assistant agents.
/// Follows the TypeScript createAssistant pattern.
/// </summary>
public static class AssistantBuilder
{
    /// <summary>
    /// Generate a unique agent ID.
    /// Format: agt:{timestamp}{random} (26 characters total)
    /// Matches the SDK's Agent.GenerateAgentId() implementation.
    /// </summary>
    public static string GenerateAgentId()
    {
        var chars = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Encode timestamp (10 characters)
        var timePart = new char[10];
        var num = now;
        for (var i = 9; i >= 0; i--)
        {
            timePart[i] = chars[(int)(num % chars.Length)];
            num /= (long)chars.Length;
        }

        // Random part (16 characters)
        var random = new char[16];
        var rand = new Random();
        for (var i = 0; i < 16; i++)
        {
            random[i] = chars[rand.Next(chars.Length)];
        }

        return $"agt_{new string(timePart)}{new string(random)}";
    }

    /// <summary>
    /// 获取Template的默认权限配置
    /// </summary>
    private static PermissionConfig GetTemplatePermissionConfig()
    {
        return new PermissionConfig
        {
            Mode = "auto",
            RequireApprovalTools =
            [
                "email_send",        // 发送邮件需审批
                "email_delete",      // 删除邮件需审批
                "fs_rm",            // 删除文件需审批
                "fs_delete",        // 删除文件需审批
                "fs_remove",        // 删除文件需审批
                "bash_run"          // 执行命令需审批
            ],
            AllowTools =
            [
                // 文件系统（只读和编辑）
                "fs_read", "fs_write", "fs_edit", "fs_grep", "fs_glob", "fs_multi_edit",
                // 邮件（读和草稿）
                "email_list", "email_read", "email_draft", "email_move",
                // 通知
                "notify_send",
                // 时间
                "time_now",
                // Skills
                "skill_list", "skill_activate", "skill_resource",
                // Todo
                "todo_read", "todo_write",
                // Bash (受限 - 仅日志)
                "bash_logs"
            ],
            DenyTools =
            [
                "bash_kill" // 禁止杀进程
            ]
        };
    }

    /// <summary>
    /// Create a Personal Assistant Agent.
    /// </summary>
    public static async Task<AgentImpl> CreateAssistantAsync(
        CreateAssistantOptions options,
        AgentDependencies globalDeps,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(AssistantBuilder).FullName!);
        var workDir = options.WorkDir ?? Directory.GetCurrentDirectory();
        // var storeDir = options.StoreDir ?? Path.Combine(workDir, ".assistant-store");

        // // Ensure store directory exists
        // Directory.CreateDirectory(storeDir);

        // Determine effective agent ID first (needed for data dir fallback)
        var effectiveAgentId = !string.IsNullOrEmpty(options.AgentId)
            ? options.AgentId!
            : GenerateAgentId();

        // Determine data directory
        // Priority: userDataDir > userId > agent-specific data (for isolation)
        var dataDir = !string.IsNullOrEmpty(options.UserDataDir)
            ? options.UserDataDir
            : !string.IsNullOrEmpty(options.UserId)
                ? Path.Combine(workDir, ".users", options.UserId!)
                : Path.Combine(workDir, "data");

        // Determine session directory (for store + sandbox internal state).
        // By default, WebApiAssistant uses a shared workspace under "<workDir>/data" and keeps per-session state under
        // "<storeDir>/<agentId>" (aligned with JsonAgentStore).
        var storeDir = !string.IsNullOrEmpty(options.StoreDir)
            ? options.StoreDir!
            : Path.Combine(workDir, ".kode");
        var resolvedStoreDir = Path.IsPathRooted(storeDir)
            ? Path.GetFullPath(storeDir)
            : Path.GetFullPath(Path.Combine(workDir, storeDir));
        var sessionDir = Path.Combine(resolvedStoreDir, effectiveAgentId);

        // Ensure data directory structure
        EnsureDataDirectory(dataDir, loggerFactory);
        Directory.CreateDirectory(sessionDir);

        // Create per-agent dependencies with tool registry
        var agentDeps = await CreateAgentDependenciesAsync(
            string.Empty, // agentId not needed for deps creation
            globalDeps,
            serviceProvider,
            loggerFactory,
            dataDir,
            options.Skills?.Paths,
            workDir);

        // Get MCP tool names
        var toolRegistry = agentDeps.ToolRegistry as Kode.Agent.Sdk.Tools.ToolRegistry;
        var allToolNames = toolRegistry?.List().ToList() ?? new List<string>();
        var mcpToolNames = allToolNames
            .Where(name => name.StartsWith("mcp__"))
            .ToList();

        // Build assistant hooks
        var hooksResult = AssistantHooks.Build(
            agentDeps,
            new BuildAssistantHooksOptions { DataDir = dataDir },
            logger);

        // var agentDir = Path.Combine(storeDir, effectiveAgentId);
        // Directory.CreateDirectory(agentDir);

        // Build allowPaths
        var allowPaths = new List<string>
        {
            // agentDir,
            dataDir,
            workDir
        };

        // Add Skills paths if configured
        if (options.Skills?.Paths != null)
        {
            foreach (var skillsPath in options.Skills.Paths)
            {
                var resolvedPath = Path.IsPathRooted(skillsPath)
                    ? skillsPath
                    : Path.Combine(workDir, skillsPath);
                if (Directory.Exists(resolvedPath))
                {
                    allowPaths.Add(resolvedPath);
                }
            }
        }

        // Merge MCP tool names into allowTools
        var mergedAllowTools = MergeAllowTools(options.Permissions?.AllowTools, mcpToolNames);

        // 获取Template的默认权限配置（直接使用内部方法）
        var templatePermissionConfig = GetTemplatePermissionConfig();

        // 合并权限配置：优先使用配置文件中的值，如果没有则使用Template默认值
        var mergedRequireApprovalTools = options.Permissions?.RequireApprovalTools?.Count > 0
            ? options.Permissions.RequireApprovalTools
            : templatePermissionConfig?.RequireApprovalTools;

        var mergedDenyTools = options.Permissions?.DenyTools?.Count > 0
            ? options.Permissions.DenyTools
            : templatePermissionConfig?.DenyTools;

        var config = new AgentConfig
        {
            Model = options.Model ?? "claude-sonnet-4-5-20250929",
            SystemPrompt = options.SystemPrompt,
            TemplateId = AssistantTemplate.PersonalAssistantTemplateId,
            MaxIterations = 50,
            MaxTokens = options.MaxTokens,
            Temperature = options.Temperature,
            Tools = ["*"], // Allow all tools
            Permissions = new PermissionConfig
            {
                Mode = options.Permissions?.Mode ?? templatePermissionConfig?.Mode ?? "auto",
                AllowTools = mergedAllowTools,
                RequireApprovalTools = mergedRequireApprovalTools,
                DenyTools = mergedDenyTools
            },
            SandboxOptions = new SandboxOptions
            {
                WorkingDirectory = dataDir,
                EnforceBoundary = true,
                AllowPaths = allowPaths,
                UseDocker = options.UseDockerSandbox,
                DockerImage = options.DockerImage,
                DockerNetworkMode = options.DockerNetworkMode,
                SandboxStateDirectory = sessionDir
            },
            Skills = options.Skills,
            Hooks = [hooksResult.Hooks]
        };

        AgentImpl agent;

        // Resume or create agent
        if (await globalDeps.Store.ExistsAsync(effectiveAgentId, cancellationToken))
        {
            var overrides = new AgentConfigOverrides
            {
                Model = config.Model,
                SystemPrompt = config.SystemPrompt,
                TemplateId = config.TemplateId,
                SandboxOptions = config.SandboxOptions,
                MaxTokens = config.MaxTokens,
                Temperature = config.Temperature,
                Tools = config.Tools,
                Permissions = config.Permissions,
                Hooks = config.Hooks,
                Skills = config.Skills
            };

            agent = await AgentImpl.ResumeFromStoreAsync(
                effectiveAgentId,
                agentDeps,
                overrides: overrides,
                cancellationToken: cancellationToken);
        }
        else
        {
            agent = await AgentImpl.CreateAsync(
                effectiveAgentId,
                config,
                agentDeps,
                cancellationToken);
        }

        logger.LogInformation("Assistant {AgentId} created/resumed successfully", effectiveAgentId);
        return agent;
    }

    /// <summary>
    /// Ensure data directory structure exists.
    /// </summary>
    private static void EnsureDataDirectory(string dataDir, ILoggerFactory loggerFactory)
    {
        var memoryDir = Path.Combine(dataDir, ".memory");
        var knowledgeDir = Path.Combine(dataDir, ".knowledge");
        var configDir = Path.Combine(dataDir, ".config");
        var tasksDir = Path.Combine(dataDir, ".tasks");

        foreach (var dir in new[] { memoryDir, knowledgeDir, configDir, tasksDir })
        {
            Directory.CreateDirectory(dir);
        }

        InitDefaultConfigs(configDir);
        InitDefaultMemory(memoryDir);
    }

    /// <summary>
    /// Initialize default configuration files.
    /// </summary>
    private static void InitDefaultConfigs(string configDir)
    {
        // Notify config
        var notifyConfigPath = Path.Combine(configDir, "notify.json");
        if (!File.Exists(notifyConfigPath))
        {
            var defaultNotifyConfig = new
            {
                @default = "dingtalk",
                channels = new
                {
                    dingtalk = new
                    {
                        webhook = "",
                        secret = ""
                    },
                    wecom = new
                    {
                        webhook = ""
                    },
                    telegram = new
                    {
                        botToken = "",
                        chatId = ""
                    }
                }
            };
            WriteJson(notifyConfigPath, defaultNotifyConfig);
        }

        // Email config
        var emailConfigPath = Path.Combine(configDir, "email.json");
        if (!File.Exists(emailConfigPath))
        {
            var defaultEmailConfig = new
            {
                imap = new
                {
                    host = "imap.gmail.com",
                    port = 993,
                    secure = true,
                    auth = new
                    {
                        user = "",
                        pass = ""
                    }
                },
                smtp = new
                {
                    host = "smtp.gmail.com",
                    port = 587,
                    secure = false,
                    auth = new
                    {
                        user = "",
                        pass = ""
                    }
                }
            };
            WriteJson(emailConfigPath, defaultEmailConfig);
        }
    }

    /// <summary>
    /// Initialize default memory structure.
    /// </summary>
    private static void InitDefaultMemory(string memoryDir)
    {
        var profilePath = Path.Combine(memoryDir, "profile.json");
        if (!File.Exists(profilePath))
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            var defaultProfile = new
            {
                assistantName = "Koda",
                userName = "",
                timezone = "Asia/Shanghai",
                language = "zh-CN",
                preferences = new { },
                createdAt = now,
                updatedAt = now
            };
            WriteJson(profilePath, defaultProfile);
        }

        Directory.CreateDirectory(Path.Combine(memoryDir, "facts"));
    }

    /// <summary>
    /// Write JSON to file with indentation.
    /// </summary>
    private static void WriteJson<T>(string path, T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Create agent-specific dependencies.
    /// </summary>
    private static async Task<AgentDependencies> CreateAgentDependenciesAsync(
        string agentId,
        AgentDependencies globalDeps,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        string dataDir,
        IReadOnlyList<string>? skillsPaths = null,
        string? workDir = null)
    {
        // Create a new tool registry for this agent
        var agentRegistry = new Kode.Agent.Sdk.Tools.ToolRegistry();

        // Register built-in tools
        agentRegistry.RegisterBuiltinTools();

        // Register platform tools (time)
        agentRegistry.RegisterPlatformTools(serviceProvider);

        // Load agent-specific tools from .config directory (email, notify)
        var agentToolsLoader = new Services.AgentToolsLoader(loggerFactory, serviceProvider);
        await agentToolsLoader.LoadAgentToolsAsync(agentId, dataDir, agentRegistry);

        // Load MCP tools (from global configuration)
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var mcpServersLoader = serviceProvider.GetRequiredService<Services.McpServersLoader>();
        await mcpServersLoader.LoadAndRegisterServersAsync(configuration, agentRegistry);

        // Get template registry from global deps
        var templateRegistry = globalDeps.TemplateRegistry;
        if (templateRegistry == null)
        {
            templateRegistry = new Sdk.Core.Templates.AgentTemplateRegistry();
            templateRegistry.Register(AssistantTemplate.CreatePersonalAssistantTemplate(skillsPaths, workDir));
        }

        return globalDeps with
        {
            ToolRegistry = agentRegistry, 
            TemplateRegistry = templateRegistry, 
            LoggerFactory = loggerFactory
        };
    }

    /// <summary>
    /// Merge allow tools with MCP tool names.
    /// </summary>
    private static IReadOnlyList<string>? MergeAllowTools(
        IReadOnlyList<string>? baseAllowTools,
        IReadOnlyList<string> mcpToolNames)
    {
        if (baseAllowTools == null) return null;
        if (baseAllowTools.Contains("*")) return baseAllowTools;

        return baseAllowTools
            .Concat(mcpToolNames)
            .Distinct()
            .ToList();
    }
}
