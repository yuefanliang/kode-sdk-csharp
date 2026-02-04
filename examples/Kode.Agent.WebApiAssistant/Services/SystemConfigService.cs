using Kode.Agent.WebApiAssistant.Services.Persistence;
using Kode.Agent.WebApiAssistant.Services.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// 系统配置服务 - 管理所有系统配置的CRUD操作
/// </summary>
public class SystemConfigService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SystemConfigService> _logger;
    private readonly IConfiguration _configuration;
    private static readonly Dictionary<string, string> _configCache = new();
    private static readonly object _cacheLock = new();

    public SystemConfigService(
        AppDbContext dbContext,
        ILogger<SystemConfigService> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// 初始化默认配置 - 将appsettings.json中的配置导入数据库
    /// </summary>
    public async Task InitializeDefaultConfigsAsync()
    {
        var defaultConfigs = GetDefaultConfigs();
        var existingKeys = await _dbContext.SystemConfigs.Select(c => c.ConfigKey).ToListAsync();
        var newKeys = new List<string>();

        foreach (var config in defaultConfigs)
        {
            if (!existingKeys.Contains(config.ConfigKey))
            {
                _dbContext.SystemConfigs.Add(config);
                newKeys.Add(config.ConfigKey);
            }
        }

        if (newKeys.Count > 0)
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Initialized {Count} default system configs: {Keys}",
                newKeys.Count, string.Join(", ", newKeys));
        }
    }

    /// <summary>
    /// 获取所有配置分组
    /// </summary>
    public async Task<List<string>> GetConfigGroupsAsync()
    {
        return await _dbContext.SystemConfigs
            .Select(c => c.Group)
            .Distinct()
            .OrderBy(g => g)
            .ToListAsync();
    }

    /// <summary>
    /// 获取分组下的所有配置
    /// </summary>
    public async Task<List<SystemConfigEntity>> GetConfigsByGroupAsync(string group)
    {
        return await _dbContext.SystemConfigs
            .Where(c => c.Group == group)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.DisplayName)
            .ToListAsync();
    }

    /// <summary>
    /// 获取所有配置
    /// </summary>
    public async Task<List<SystemConfigEntity>> GetAllConfigsAsync()
    {
        return await _dbContext.SystemConfigs
            .OrderBy(c => c.Group)
            .ThenBy(c => c.SortOrder)
            .ToListAsync();
    }

    /// <summary>
    /// 获取单个配置值
    /// </summary>
    public async Task<string?> GetConfigAsync(string key)
    {
        // 先从缓存获取
        lock (_cacheLock)
        {
            if (_configCache.TryGetValue(key, out var cachedValue))
            {
                return cachedValue;
            }
        }

        // 从数据库获取
        var config = await _dbContext.SystemConfigs
            .FirstOrDefaultAsync(c => c.ConfigKey == key);

        if (config == null)
        {
            // 尝试从appsettings.json获取
            var value = _configuration[key];
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
            return null;
        }

        var result = config.IsEncrypted ? Decrypt(config.ConfigValue) : config.ConfigValue;

        // 存入缓存
        lock (_cacheLock)
        {
            _configCache[key] = result ?? string.Empty;
        }

        return result;
    }

    /// <summary>
    /// 获取配置值（带类型转换）
    /// </summary>
    public async Task<T?> GetConfigAsync<T>(string key)
    {
        var value = await GetConfigAsync(key);
        if (string.IsNullOrEmpty(value)) return default;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// 更新配置值
    /// </summary>
    public async Task<bool> UpdateConfigAsync(string key, string? value)
    {
        var config = await _dbContext.SystemConfigs
            .FirstOrDefaultAsync(c => c.ConfigKey == key);

        if (config == null || !config.IsEditable)
        {
            return false;
        }

        config.ConfigValue = config.IsEncrypted ? Encrypt(value) : value;
        config.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        // 更新缓存
        lock (_cacheLock)
        {
            _configCache[key] = value ?? string.Empty;
        }

        _logger.LogInformation("Updated config: {Key}", key);
        return true;
    }

    /// <summary>
    /// 批量更新配置
    /// </summary>
    public async Task<bool> UpdateConfigsAsync(Dictionary<string, string?> configs)
    {
        foreach (var (key, value) in configs)
        {
            var config = await _dbContext.SystemConfigs
                .FirstOrDefaultAsync(c => c.ConfigKey == key);

            if (config != null && config.IsEditable)
            {
                config.ConfigValue = config.IsEncrypted ? Encrypt(value) : value;
                config.UpdatedAt = DateTime.UtcNow;

                // 更新缓存
                lock (_cacheLock)
                {
                    _configCache[key] = value ?? string.Empty;
                }
            }
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Batch updated {Count} configs", configs.Count);
        return true;
    }

    /// <summary>
    /// 清除配置缓存
    /// </summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            _configCache.Clear();
        }
    }

    /// <summary>
    /// 获取默认配置定义
    /// </summary>
    private List<SystemConfigEntity> GetDefaultConfigs()
    {
        return new List<SystemConfigEntity>
        {
            // AI Provider 配置组
            new() {
                ConfigKey = "Kode:DefaultProvider",
                ConfigValue = _configuration["Kode:DefaultProvider"] ?? "openai",
                Group = "AI Provider",
                DisplayName = "默认AI提供商",
                Description = "选择默认使用的AI提供商（openai, anthropic）",
                ValueType = "select",
                Options = "[\"openai\", \"anthropic\"]",
                SortOrder = 1
            },
            new() {
                ConfigKey = "Kode:DefaultModel",
                ConfigValue = _configuration["Kode:DefaultModel"] ?? "",
                Group = "AI Provider",
                DisplayName = "默认模型",
                Description = "设置默认使用的AI模型ID",
                ValueType = "string",
                SortOrder = 2
            },

            // OpenAI 配置组
            new() {
                ConfigKey = "Kode:OpenAI:ApiKey",
                ConfigValue = _configuration["Kode:OpenAI:ApiKey"] ?? "",
                Group = "OpenAI",
                DisplayName = "API密钥",
                Description = "OpenAI API密钥",
                ValueType = "password",
                IsEncrypted = true,
                SortOrder = 1
            },
            new() {
                ConfigKey = "Kode:OpenAI:BaseUrl",
                ConfigValue = _configuration["Kode:OpenAI:BaseUrl"] ?? "https://api.openai.com",
                Group = "OpenAI",
                DisplayName = "API地址",
                Description = "OpenAI API基础URL",
                ValueType = "string",
                SortOrder = 2
            },

            // Anthropic 配置组
            new() {
                ConfigKey = "Kode:Anthropic:ApiKey",
                ConfigValue = _configuration["Kode:Anthropic:ApiKey"] ?? "",
                Group = "Anthropic",
                DisplayName = "API密钥",
                Description = "Anthropic API密钥",
                ValueType = "password",
                IsEncrypted = true,
                SortOrder = 1
            },
            new() {
                ConfigKey = "Kode:Anthropic:BaseUrl",
                ConfigValue = _configuration["Kode:Anthropic:BaseUrl"] ?? "https://api.anthropic.com",
                Group = "Anthropic",
                DisplayName = "API地址",
                Description = "Anthropic API基础URL",
                ValueType = "string",
                SortOrder = 2
            },
            new() {
                ConfigKey = "Kode:Anthropic:ModelId",
                ConfigValue = _configuration["Kode:Anthropic:ModelId"] ?? "claude-sonnet-4-20250514",
                Group = "Anthropic",
                DisplayName = "模型ID",
                Description = "默认使用的Claude模型ID",
                ValueType = "string",
                SortOrder = 3
            },

            // 系统配置组
            new() {
                ConfigKey = "Kode:SystemPrompt",
                ConfigValue = _configuration["Kode:SystemPrompt"] ?? "You are a helpful personal assistant.",
                Group = "System",
                DisplayName = "系统提示词",
                Description = "AI助手的默认系统提示词",
                ValueType = "textarea",
                SortOrder = 1
            },
            new() {
                ConfigKey = "Kode:Tools",
                ConfigValue = _configuration["Kode:Tools"] ?? "fs_read,fs_list,fs_glob,fs_grep",
                Group = "System",
                DisplayName = "启用的工具",
                Description = "逗号分隔的可用工具列表",
                ValueType = "string",
                SortOrder = 2
            },
            new() {
                ConfigKey = "Kode:PermissionMode",
                ConfigValue = _configuration["Kode:PermissionMode"] ?? "auto",
                Group = "System",
                DisplayName = "权限模式",
                Description = "工具执行权限模式（auto/approval/readonly）",
                ValueType = "select",
                Options = "[\"auto\", \"approval\", \"readonly\"]",
                SortOrder = 3
            },

            // 工具权限配置组
            new() {
                ConfigKey = "Kode:AllowTools",
                ConfigValue = _configuration["Kode:AllowTools"] ?? "*",
                Group = "Tool Permissions",
                DisplayName = "允许的工具",
                Description = "允许使用的工具列表，*表示全部",
                ValueType = "string",
                SortOrder = 1
            },
            new() {
                ConfigKey = "Kode:RequireApprovalTools",
                ConfigValue = _configuration["Kode:RequireApprovalTools"] ?? "email_send,email_delete,fs_rm,fs_delete",
                Group = "Tool Permissions",
                DisplayName = "需要审批的工具",
                Description = "需要用户审批才能执行的工具",
                ValueType = "string",
                SortOrder = 2
            },
            new() {
                ConfigKey = "Kode:DenyTools",
                ConfigValue = _configuration["Kode:DenyTools"] ?? "bash_kill",
                Group = "Tool Permissions",
                DisplayName = "禁止的工具",
                Description = "禁止使用的工具",
                ValueType = "string",
                SortOrder = 3
            },

            // Skill 配置组
            new() {
                ConfigKey = "Kode:SkillsDir",
                ConfigValue = _configuration["Kode:SkillsDir"] ?? "skills",
                Group = "Skills",
                DisplayName = "Skill目录",
                Description = "Skill文件存放目录",
                ValueType = "string",
                SortOrder = 1
            },
            new() {
                ConfigKey = "Kode:TrustedSkills",
                ConfigValue = _configuration["Kode:TrustedSkills"] ?? "memory,knowledge,email",
                Group = "Skills",
                DisplayName = "信任的Skill",
                Description = "默认信任的Skill列表",
                ValueType = "string",
                SortOrder = 2
            },

            // Sandbox 配置组
            new() {
                ConfigKey = "Kode:Sandbox:UseDocker",
                ConfigValue = _configuration["Kode:Sandbox:UseDocker"] ?? "false",
                Group = "Sandbox",
                DisplayName = "使用Docker沙箱",
                Description = "是否使用Docker作为命令执行沙箱",
                ValueType = "boolean",
                SortOrder = 1
            },
            new() {
                ConfigKey = "Kode:Sandbox:DockerImage",
                ConfigValue = _configuration["Kode:Sandbox:DockerImage"] ?? "kode-agent-sandbox:0.1.0",
                Group = "Sandbox",
                DisplayName = "Docker镜像",
                Description = "沙箱使用的Docker镜像",
                ValueType = "string",
                SortOrder = 2
            },

            // Agent Pool 配置组
            new() {
                ConfigKey = "Kode:AgentPool:Enabled",
                ConfigValue = _configuration["Kode:AgentPool:Enabled"] ?? "auto",
                Group = "Agent Pool",
                DisplayName = "启用Agent池",
                Description = "是否启用Agent池缓存",
                ValueType = "select",
                Options = "[\"auto\", \"true\", \"false\"]",
                SortOrder = 1
            },
            new() {
                ConfigKey = "Kode:AgentPool:MaxAgents",
                ConfigValue = _configuration["Kode:AgentPool:MaxAgents"] ?? "50",
                Group = "Agent Pool",
                DisplayName = "最大Agent数",
                Description = "Agent池最大缓存数量",
                ValueType = "int",
                SortOrder = 2
            },
            new() {
                ConfigKey = "Kode:AgentPool:IdleMinutes",
                ConfigValue = _configuration["Kode:AgentPool:IdleMinutes"] ?? "20",
                Group = "Agent Pool",
                DisplayName = "空闲超时(分钟)",
                Description = "Agent空闲超时时间",
                ValueType = "int",
                SortOrder = 3
            },

            // 文件上传配置组
            new() {
                ConfigKey = "Kode:FileUpload:MaxFileSize",
                ConfigValue = _configuration["Kode:FileUpload:MaxFileSize"] ?? "104857600",
                Group = "File Upload",
                DisplayName = "最大文件大小(字节)",
                Description = "允许上传的最大文件大小",
                ValueType = "int",
                SortOrder = 1
            },
            new() {
                ConfigKey = "Kode:FileUpload:AllowedExtensions",
                ConfigValue = _configuration["Kode:FileUpload:AllowedExtensions"] ?? ".txt,.md,.pdf,.png,.jpg,.jpeg,.json,.csv",
                Group = "File Upload",
                DisplayName = "允许的文件类型",
                Description = "逗号分隔的允许上传文件扩展名",
                ValueType = "string",
                SortOrder = 2
            }
        };
    }

    private string? Encrypt(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        // 简单的加密实现，生产环境应使用更安全的加密方式
        try
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return Convert.ToBase64String(bytes);
        }
        catch
        {
            return value;
        }
    }

    private string? Decrypt(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        try
        {
            var bytes = Convert.FromBase64String(value);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return value;
        }
    }
}
