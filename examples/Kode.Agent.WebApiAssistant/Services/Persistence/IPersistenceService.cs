using Kode.Agent.WebApiAssistant.Services.Persistence.Entities;

namespace Kode.Agent.WebApiAssistant.Services.Persistence;

/// <summary>
/// 持久化服务接口
/// </summary>
public interface IPersistenceService
{
    /// <summary>
    /// 初始化数据库
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    #region User 操作

    /// <summary>
    /// 获取用户
    /// </summary>
    Task<UserEntity?> GetUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建或更新用户
    /// </summary>
    Task<UserEntity> UpsertUserAsync(UserEntity user, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除用户
    /// </summary>
    Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户列表
    /// </summary>
    Task<IReadOnlyList<UserEntity>> ListUsersAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Session 操作

    /// <summary>
    /// 获取会话
    /// </summary>
    Task<SessionEntity?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建会话
    /// </summary>
    Task<SessionEntity> CreateSessionAsync(SessionEntity session, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新会话
    /// </summary>
    Task UpdateSessionAsync(SessionEntity session, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除会话
    /// </summary>
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的所有会话
    /// </summary>
    Task<IReadOnlyList<SessionEntity>> ListSessionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 增加会话消息计数
    /// </summary>
    Task IncrementSessionMessageCountAsync(string sessionId, CancellationToken cancellationToken = default);

    #endregion

    #region Workspace 操作

    /// <summary>
    /// 获取工作区
    /// </summary>
    Task<WorkspaceEntity?> GetWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建工作区
    /// </summary>
    Task<WorkspaceEntity> CreateWorkspaceAsync(WorkspaceEntity workspace, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新工作区
    /// </summary>
    Task UpdateWorkspaceAsync(WorkspaceEntity workspace, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除工作区
    /// </summary>
    Task DeleteWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的所有工作区
    /// </summary>
    Task<IReadOnlyList<WorkspaceEntity>> ListWorkspacesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置用户的活动工作区
    /// </summary>
    Task SetActiveWorkspaceAsync(string userId, string workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的活动工作区
    /// </summary>
    Task<WorkspaceEntity?> GetActiveWorkspaceAsync(string userId, CancellationToken cancellationToken = default);

    #endregion
}
