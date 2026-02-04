namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// 审批服务接口
/// </summary>
public interface IApprovalService
{
    /// <summary>
    /// 创建审批请求
    /// </summary>
    Task<string> CreateApprovalAsync(
        string agentId,
        string userId,
        string toolName,
        object? arguments = null);

    /// <summary>
    /// 获取审批详情
    /// </summary>
    Task<Models.Entities.Approval?> GetApprovalAsync(string approvalId);

    /// <summary>
    /// 获取用户的待审批列表
    /// </summary>
    Task<IReadOnlyList<Models.Entities.Approval>> GetPendingApprovalsAsync(string userId);

    /// <summary>
    /// 确认审批
    /// </summary>
    Task<bool> ConfirmApprovalAsync(
        string approvalId,
        string userId,
        string? note = null);

    /// <summary>
    /// 取消审批
    /// </summary>
    Task<bool> CancelApprovalAsync(
        string approvalId,
        string userId,
        string? note = null);

    /// <summary>
    /// 注册审批回调函数
    /// </summary>
    void RegisterApprovalCallback(string callId, Func<string, object?, Task> callback);

    /// <summary>
    /// 执行工具审批操作（简化版本，不需要验证审批人）
    /// </summary>
    Task<bool> PerformToolApprovalActionAsync(
        string approvalId,
        string action,
        string? comment = null);
}
