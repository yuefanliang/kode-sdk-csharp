namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// 工作区管理服务接口
/// </summary>
public interface IWorkspaceService
{
    /// <summary>
    /// 创建工作区
    /// </summary>
    Task<Models.Entities.Workspace> CreateWorkspaceAsync(
        string userId,
        string name,
        string? description = null,
        string? workDir = null);

    /// <summary>
    /// 获取工作区详情
    /// </summary>
    Task<Models.Entities.Workspace?> GetWorkspaceAsync(string workspaceId);

    /// <summary>
    /// 获取用户的所有工作区
    /// </summary>
    Task<IReadOnlyList<Models.Entities.Workspace>> ListWorkspacesAsync(string userId);

    /// <summary>
    /// 更新工作区
    /// </summary>
    Task<Models.Entities.Workspace?> UpdateWorkspaceAsync(
        string workspaceId,
        string? name = null,
        string? description = null,
        string? workDir = null);

    /// <summary>
    /// 删除工作区
    /// </summary>
    Task DeleteWorkspaceAsync(string workspaceId);

    /// <summary>
    /// 设置活动工作区
    /// </summary>
    Task SetActiveWorkspaceAsync(string userId, string workspaceId);

    /// <summary>
    /// 获取用户的活动工作区
    /// </summary>
    Task<Models.Entities.Workspace?> GetActiveWorkspaceAsync(string userId);

    /// <summary>
    /// 为会话分配工作区
    /// </summary>
    Task AssignSessionToWorkspaceAsync(string sessionId, string workspaceId);
}
