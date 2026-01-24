using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.WebApiAssistant.Models.Entities;
using Kode.Agent.WebApiAssistant.Services.Persistence;
using Kode.Agent.WebApiAssistant.Services.Persistence.Entities;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// 用户管理服务实现
/// </summary>
public class UserService : IUserService
{
    private readonly IAgentStore _store;
    private readonly ILogger<UserService> _logger;
    private readonly ConcurrentDictionary<string, User> _cache = new();
    private readonly IPersistenceService _persistenceService;

    public UserService(
        IAgentStore store,
        ILogger<UserService> logger,
        IPersistenceService persistenceService)
    {
        _store = store;
        _logger = logger;
        _persistenceService = persistenceService;
    }

    public async Task<User?> GetUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        // 先从缓存获取
        if (_cache.TryGetValue(userId, out var cachedUser))
        {
            return cachedUser;
        }

        // 从持久化存储加载
        var userEntity = await _persistenceService.GetUserAsync(userId);
        if (userEntity != null)
        {
            var user = MapToUser(userEntity);
            _cache[userId] = user;
            _logger.LogInformation("Loaded user from persistence: {UserId}", userId);
            return user;
        }

        return null;
    }

    public async Task<User> GetOrCreateUserAsync(string userId)
    {
        var user = await GetUserAsync(userId);
        if (user != null)
        {
            // 更新最后活跃时间
            user.LastActiveAt = DateTime.UtcNow;
            _cache[userId] = user;

            // 持久化更新
            var userEntity = MapToUserEntity(user);
            await _persistenceService.UpsertUserAsync(userEntity);
            return user;
        }

        // 创建新用户
        user = new User
        {
            UserId = userId,
            DisplayName = userId,
            AgentId = await GenerateAgentIdAsync(userId),
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };

        _cache[userId] = user;
        _logger.LogInformation("Created new user: {UserId}", userId);

        // 持久化保存
        var persistEntity = MapToUserEntity(user);
        await _persistenceService.UpsertUserAsync(persistEntity);

        return user;
    }

    public async Task<string> GetAgentIdAsync(string userId)
    {
        var user = await GetOrCreateUserAsync(userId);
        return user.AgentId;
    }

    /// <summary>
    /// 生成用户的 Agent ID
    /// </summary>
    private async Task<string> GenerateAgentIdAsync(string userId)
    {
        // 使用用户 ID 作为基础，添加平台信息
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos" :
                      RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
                      RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "unknown";

        var agentId = $"user_{userId}_{platform}";

        // 检查 Agent 是否已存在，如果存在则返回已存在的 ID
        if (await _store.ExistsAsync(agentId))
        {
            return agentId;
        }

        return agentId;
    }

    /// <summary>
    /// 映射 UserEntity 到 User
    /// </summary>
    private static User MapToUser(UserEntity entity)
    {
        return new User
        {
            UserId = entity.UserId,
            DisplayName = entity.DisplayName,
            AgentId = entity.AgentId,
            CreatedAt = entity.CreatedAt,
            LastActiveAt = entity.LastActiveAt
        };
    }

    /// <summary>
    /// 映射 User 到 UserEntity
    /// </summary>
    private static UserEntity MapToUserEntity(User user)
    {
        return new UserEntity
        {
            UserId = user.UserId,
            DisplayName = user.DisplayName,
            AgentId = user.AgentId,
            CreatedAt = user.CreatedAt,
            LastActiveAt = user.LastActiveAt
        };
    }
}
