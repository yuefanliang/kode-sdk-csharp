using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.WebApiAssistant.Models.Entities;
using Kode.Agent.WebApiAssistant.Services.Persistence;
using Kode.Agent.WebApiAssistant.Services.Persistence.Entities;
using System.Collections.Concurrent;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// 会话管理服务实现
/// </summary>
public class SessionService : ISessionService
{
    private readonly IAgentStore _store;
    private readonly ILogger<SessionService> _logger;
    private readonly ConcurrentDictionary<string, Session> _cache = new();
    private readonly IPersistenceService _persistenceService;
    private readonly IUserService _userService;

    private const string WORKSPACE_BASE_DIR = "./session-workspaces";

    public SessionService(
        IAgentStore store,
        ILogger<SessionService> logger,
        IPersistenceService persistenceService,
        IUserService userService)
    {
        _store = store;
        _logger = logger;
        _persistenceService = persistenceService;
        _userService = userService;
    }

    public async Task<Session> CreateSessionAsync(string userId, string? title = null)
    {
        // 验证用户是否存在
        var user = await _userService.GetUserAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User not found when creating session: {UserId}", userId);
            throw new ArgumentException($"User not found: {userId}", nameof(userId));
        }

        var sessionId = Guid.NewGuid().ToString("N");
        var agentId = $"session_{sessionId}";

        var session = new Session
        {
            SessionId = sessionId,
            UserId = userId,
            Title = title ?? "新对话",
            AgentId = agentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            MessageCount = 0
        };

        _cache[sessionId] = session;

        // 持久化保存会话
        var sessionEntity = MapToSessionEntity(session);
        await _persistenceService.CreateSessionAsync(sessionEntity);

        // 创建会话工作区目录
        var workDirectory = Path.Combine(WORKSPACE_BASE_DIR, sessionId);
        if (!Directory.Exists(workDirectory))
        {
            Directory.CreateDirectory(workDirectory);
            _logger.LogInformation("Created workspace directory: {WorkDir}", workDirectory);
        }

        // 持久化保存会话工作区配置
        var workspaceEntity = new Kode.Agent.WebApiAssistant.Services.Persistence.Entities.SessionWorkspaceEntity
        {
            WorkspaceId = Guid.NewGuid().ToString("N"),
            SessionId = sessionId,
            UserId = userId,
            WorkDirectory = workDirectory,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };
        await _persistenceService.UpsertSessionWorkspaceAsync(workspaceEntity);

        _logger.LogInformation("Created new session: {SessionId} for user: {UserId} with workspace: {WorkDir}",
            sessionId, userId, workDirectory);

        return session;
    }

    public async Task<Session?> GetSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        // 从缓存获取
        if (_cache.TryGetValue(sessionId, out var cachedSession))
        {
            return cachedSession;
        }

        // 从持久化存储加载
        var sessionEntity = await _persistenceService.GetSessionAsync(sessionId);
        if (sessionEntity != null)
        {
            var session = MapToSession(sessionEntity);
            _cache[sessionId] = session;
            return session;
        }

        return null;
    }

    public async Task<IReadOnlyList<Session>> ListSessionsAsync(string userId)
    {
        // 从持久化存储加载
        var sessionEntities = await _persistenceService.ListSessionsAsync(userId);
        var sessions = sessionEntities
            .Select(MapToSession)
            .OrderByDescending(s => s.UpdatedAt)
            .ToList();

        // 更新缓存
        foreach (var session in sessions)
        {
            _cache[session.SessionId] = session;
        }

        return sessions;
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        // 从持久化存储删除
        await _persistenceService.DeleteSessionAsync(sessionId);

        // 从缓存移除
        if (_cache.TryRemove(sessionId, out _))
        {
            _logger.LogInformation("Deleted session: {SessionId}", sessionId);
        }
    }

    public async Task UpdateSessionTitleAsync(string sessionId, string title)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        if (_cache.TryGetValue(sessionId, out var session))
        {
            session.Title = title;
            session.UpdatedAt = DateTime.UtcNow;

            // 持久化更新
            var sessionEntity = MapToSessionEntity(session);
            await _persistenceService.UpdateSessionAsync(sessionEntity);

            _logger.LogInformation("Updated session title: {SessionId}", sessionId);
        }
    }

    public async Task<Session> GetOrCreateSessionAsync(string userId, string? sessionId = null)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var existingSession = await GetSessionAsync(sessionId);
            if (existingSession != null && existingSession.UserId == userId)
            {
                existingSession.UpdatedAt = DateTime.UtcNow;
                return existingSession;
            }
        }

        // 列出用户的所有会话
        var sessions = await ListSessionsAsync(userId);
        if (sessions.Count > 0)
        {
            // 返回最近更新的会话
            var latestSession = sessions.OrderByDescending(s => s.UpdatedAt).First();
            latestSession.UpdatedAt = DateTime.UtcNow;
            return latestSession;
        }

        // 创建新会话
        return await CreateSessionAsync(userId);
    }

    /// <summary>
    /// 增加会话消息计数
    /// </summary>
    public async Task IncrementMessageCountAsync(string sessionId)
    {
        if (_cache.TryGetValue(sessionId, out var session))
        {
            session.MessageCount++;
            session.UpdatedAt = DateTime.UtcNow;

            // 持久化更新
            await _persistenceService.IncrementSessionMessageCountAsync(sessionId);
        }
    }

    /// <summary>
    /// 映射 SessionEntity 到 Session
    /// </summary>
    private static Session MapToSession(SessionEntity entity)
    {
        return new Session
        {
            SessionId = entity.SessionId,
            UserId = entity.UserId,
            Title = entity.Title,
            AgentId = entity.AgentId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            MessageCount = entity.MessageCount
        };
    }

    /// <summary>
    /// 映射 Session 到 SessionEntity
    /// </summary>
    private static SessionEntity MapToSessionEntity(Session session)
    {
        return new SessionEntity
        {
            SessionId = session.SessionId,
            UserId = session.UserId,
            Title = session.Title,
            AgentId = session.AgentId,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            MessageCount = session.MessageCount
        };
    }
}
