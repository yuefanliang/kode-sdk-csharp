using Kode.Agent.Sdk.Core.Agent;
using System.Collections.Concurrent;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// 审批服务实现
/// </summary>
public class ApprovalService : IApprovalService
{
    private readonly ILogger<ApprovalService> _logger;
    private readonly ConcurrentDictionary<string, Models.Entities.Approval> _approvals = new();
    private readonly ConcurrentDictionary<string, Func<string, object?, Task>> _approvalCallbacks = new();
    private readonly AssistantAgentPool? _agentPool;

    public ApprovalService(
        ILogger<ApprovalService> logger,
        AssistantAgentPool? agentPool = null)
    {
        _logger = logger;
        _agentPool = agentPool;
    }

    public Task<string> CreateApprovalAsync(
        string agentId,
        string userId,
        string toolName,
        object? arguments = null)
    {
        var approvalId = Guid.NewGuid().ToString("N");

        // 获取工具操作信息
        var (isSensitive, operationType) = SensitiveToolManager.GetToolOperationInfo(toolName);

        // Extract callId from arguments if available
        var callId = ExtractCallId(arguments);

        var approval = new Models.Entities.Approval
        {
            ApprovalId = approvalId,
            AgentId = agentId,
            ToolName = toolName,
            Arguments = arguments,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            Decision = "pending",
            IsSensitive = isSensitive,
            OperationType = operationType,
            SessionId = agentId,
            CallId = callId
        };

        _approvals[approvalId] = approval;

        var sensitiveText = isSensitive ? " [敏感操作]" : "";
        _logger.LogInformation(
            "Created approval request: {ApprovalId} for tool: {ToolName}{SensitiveText} by user: {UserId}, callId: {CallId}",
            approvalId, toolName, sensitiveText, userId, callId);

        return Task.FromResult(approvalId);
    }

    /// <summary>
    /// 注册审批回调函数
    /// </summary>
    public void RegisterApprovalCallback(string callId, Func<string, object?, Task> callback)
    {
        if (!string.IsNullOrEmpty(callId))
        {
            _approvalCallbacks[callId!] = callback;
            _logger.LogInformation("Registered approval callback for callId: {CallId}", callId);
        }
    }

    /// <summary>
    /// 从参数中提取 callId
    /// </summary>
    private static string? ExtractCallId(object? arguments)
    {
        if (arguments == null) return null;

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(arguments);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("approvalId", out var approvalIdProp))
            {
                return approvalIdProp.GetString();
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    public Task<Models.Entities.Approval?> GetApprovalAsync(string approvalId)
    {
        _approvals.TryGetValue(approvalId, out var approval);
        return Task.FromResult(approval);
    }

    public Task<IReadOnlyList<Models.Entities.Approval>> GetPendingApprovalsAsync(string userId)
    {
        var pendingApprovals = _approvals.Values
            .Where(a => a.UserId == userId && a.Decision == "pending")
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<Models.Entities.Approval>>(pendingApprovals);
    }

    public Task<bool> ConfirmApprovalAsync(
        string approvalId,
        string userId,
        string? note = null)
    {
        if (!_approvals.TryGetValue(approvalId, out var approval))
        {
            _logger.LogWarning("Approval not found: {ApprovalId}", approvalId);
            return Task.FromResult(false);
        }

        if (approval.UserId != userId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to approve approval {ApprovalId} belonging to {OwnerUserId}",
                userId, approvalId, approval.UserId);
            return Task.FromResult(false);
        }

        if (approval.Decision != "pending")
        {
            _logger.LogWarning(
                "Approval {ApprovalId} already decided: {Decision}",
                approvalId, approval.Decision);
            return Task.FromResult(false);
        }

        approval.Decision = "approved";
        approval.DecidedAt = DateTime.UtcNow;
        approval.DecidedBy = userId;
        approval.Note = note;

        _logger.LogInformation(
            "Approved approval: {ApprovalId} for tool: {ToolName} by user: {UserId}",
            approvalId, approval.ToolName, userId);

        // 触发 Agent 继续执行
        _ = TriggerAgentDecisionAsync(approvalId);

        return Task.FromResult(true);
    }

    public Task<bool> CancelApprovalAsync(
        string approvalId,
        string userId,
        string? note = null)
    {
        if (!_approvals.TryGetValue(approvalId, out var approval))
        {
            _logger.LogWarning("Approval not found: {ApprovalId}", approvalId);
            return Task.FromResult(false);
        }

        if (approval.UserId != userId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to cancel approval {ApprovalId} belonging to {OwnerUserId}",
                userId, approvalId, approval.UserId);
            return Task.FromResult(false);
        }

        if (approval.Decision != "pending")
        {
            _logger.LogWarning(
                "Approval {ApprovalId} already decided: {Decision}",
                approvalId, approval.Decision);
            return Task.FromResult(false);
        }

        approval.Decision = "denied";
        approval.DecidedAt = DateTime.UtcNow;
        approval.DecidedBy = userId;
        approval.Note = note;

        _logger.LogInformation(
            "Denied approval: {ApprovalId} for tool: {ToolName} by user: {UserId}",
            approvalId, approval.ToolName, userId);

        // 触发 Agent 拒决执行
        _ = TriggerAgentDecisionAsync(approvalId);

        return Task.FromResult(true);
    }

    /// <summary>
    /// 触发Agent审批决策
    /// </summary>
    private async Task TriggerAgentDecisionAsync(string approvalId)
    {
        try
        {
            if (!_approvals.TryGetValue(approvalId, out var approval))
            {
                _logger.LogWarning("Approval not found: {ApprovalId}", approvalId);
                return;
            }

            var callId = approval.CallId;
            if (string.IsNullOrEmpty(callId))
            {
                _logger.LogWarning("Cannot trigger agent decision: callId is empty for approval: {ApprovalId}", approvalId);
                return;
            }

            if (!_approvalCallbacks.TryRemove(callId, out var callback))
            {
                _logger.LogWarning("Approval callback not found for callId: {CallId}", callId);
                return;
            }

            var approved = string.Equals(approval.Decision, "approved", StringComparison.OrdinalIgnoreCase);
            var decision = approved ? "allow" : "deny";

            _logger.LogInformation(
                "Triggering agent decision for approval: {ApprovalId}, callId: {CallId}, decision: {Decision}",
                approvalId, callId, decision);

            var options = approval.Note != null ? new { note = approval.Note } : null;
            await callback(decision, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger agent decision for approval: {ApprovalId}", approvalId);
        }
    }

    public async Task<bool> PerformToolApprovalActionAsync(
        string approvalId,
        string action,
        string? comment = null)
    {
        if (!_approvals.TryGetValue(approvalId, out var approval))
        {
            _logger.LogWarning("Approval not found: {ApprovalId}", approvalId);
            return false;
        }

        if (approval.Decision != "pending")
        {
            _logger.LogWarning(
                "Approval {ApprovalId} already decided: {Decision}",
                approvalId, approval.Decision);
            return false;
        }

        // 根据操作更新审批状态
        switch (action.ToLowerInvariant())
        {
            case "approve":
                approval.Decision = "approved";
                approval.DecidedAt = DateTime.UtcNow;
                approval.DecidedBy = "system";
                approval.Note = comment ?? "用户批准";
                break;

            case "reject":
                approval.Decision = "denied";
                approval.DecidedAt = DateTime.UtcNow;
                approval.DecidedBy = "system";
                approval.Note = comment ?? "用户拒绝执行";
                break;

            default:
                throw new ArgumentException($"Invalid action: {action}");
        }

        _logger.LogInformation("Tool approval {ApprovalId} action {Action}", approvalId, action);

        // 触发 Agent 决策
        await TriggerAgentDecisionAsync(approvalId);

        return true;
    }
}
