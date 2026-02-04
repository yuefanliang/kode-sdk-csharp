using Kode.Agent.WebApiAssistant.Models.Entities;
using Kode.Agent.WebApiAssistant.Services.Persistence;
using Kode.Agent.WebApiAssistant.Services.Persistence.Entities;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// 会话工作区配置服务
/// </summary>
public interface ISessionWorkspaceService
{
    /// <summary>
    /// 获取会话的工作区配置
    /// </summary>
    Task<SessionWorkspace?> GetSessionWorkspaceAsync(string sessionId, string userId);

    /// <summary>
    /// 设置会话的工作区
    /// </summary>
    Task<SessionWorkspace> SetSessionWorkspaceAsync(string sessionId, string userId, string workDirectory);

    /// <summary>
    /// 获取用户的所有工作区配置
    /// </summary>
    Task<IReadOnlyList<SessionWorkspace>> GetUserWorkspacesAsync(string userId);

    /// <summary>
    /// 删除会话工作区配置
    /// </summary>
    Task<bool> DeleteSessionWorkspaceAsync(string sessionId, string userId);

    /// <summary>
    /// 验证工作目录是否有效
    /// </summary>
    Task<(bool IsValid, string? Error)> ValidateWorkDirectoryAsync(string workDirectory, string userId);
}

/// <summary>
/// 会话工作区配置服务实现
/// </summary>
public class SessionWorkspaceService : ISessionWorkspaceService
{
    private readonly ILogger<SessionWorkspaceService> _logger;
    private readonly IPersistenceService _persistenceService;

    public SessionWorkspaceService(
        ILogger<SessionWorkspaceService> logger,
        IPersistenceService persistenceService)
    {
        _logger = logger;
        _persistenceService = persistenceService;
    }

    public async Task<SessionWorkspace?> GetSessionWorkspaceAsync(string sessionId, string userId)
    {
        var entity = await _persistenceService.GetSessionWorkspaceAsync(sessionId, userId);
        if (entity == null)
        {
            return null;
        }

        return MapToSessionWorkspace(entity);
    }

    public async Task<SessionWorkspace> SetSessionWorkspaceAsync(string sessionId, string userId, string workDirectory)
    {
        // 验证工作目录
        var (isValid, error) = await ValidateWorkDirectoryAsync(workDirectory, userId);
        if (!isValid)
        {
            throw new ArgumentException(error, nameof(workDirectory));
        }

        var workspaceEntity = new SessionWorkspaceEntity
        {
            WorkspaceId = Guid.NewGuid().ToString("N"),
            SessionId = sessionId,
            UserId = userId,
            WorkDirectory = workDirectory,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var entity = await _persistenceService.UpsertSessionWorkspaceAsync(workspaceEntity);

        _logger.LogInformation(
            "Set session workspace: {SessionId} for user: {UserId}, workDir: {WorkDir}",
            sessionId, userId, workDirectory);

        return MapToSessionWorkspace(entity);
    }

    public async Task<IReadOnlyList<SessionWorkspace>> GetUserWorkspacesAsync(string userId)
    {
        var entities = await _persistenceService.ListSessionWorkspacesAsync(userId);
        var workspaces = entities.Select(MapToSessionWorkspace).ToList();
        return workspaces;
    }

    public async Task<bool> DeleteSessionWorkspaceAsync(string sessionId, string userId)
    {
        await _persistenceService.DeleteSessionWorkspaceAsync(sessionId);
        return true;
    }

    public Task<(bool IsValid, string? Error)> ValidateWorkDirectoryAsync(string workDirectory, string userId)
    {
        var (isValid, error) = ValidateWorkDirectory(workDirectory);
        return Task.FromResult((isValid, error));
    }

    /// <summary>
    /// 验证工作目录
    /// </summary>
    private static (bool IsValid, string? Error) ValidateWorkDirectory(string workDirectory)
    {
        if (string.IsNullOrWhiteSpace(workDirectory))
        {
            return (false, "工作目录不能为空");
        }

        // 检查路径格式
        try
        {
            var normalizedPath = workDirectory.Trim();

            // 检查非法字符
            var invalidChars = Path.GetInvalidPathChars();
            if (normalizedPath.IndexOfAny(invalidChars) >= 0)
            {
                return (false, "工作目录包含非法字符");
            }

            // 检查路径长度
            if (normalizedPath.Length > 512)
            {
                return (false, "工作目录路径过长（最大512字符）");
            }

            // 尝试规范路径
            var fullPath = Path.GetFullPath(normalizedPath);

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"工作目录路径无效：{ex.Message}");
        }
    }

    /// <summary>
    /// 映射 SessionWorkspaceEntity 到 SessionWorkspace
    /// </summary>
    private static SessionWorkspace MapToSessionWorkspace(SessionWorkspaceEntity entity)
    {
        return new SessionWorkspace
        {
            WorkspaceId = entity.WorkspaceId,
            SessionId = entity.SessionId,
            UserId = entity.UserId,
            WorkDirectory = entity.WorkDirectory,
            IsDefault = entity.IsDefault,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
