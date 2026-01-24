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

    public ApprovalService(ILogger<ApprovalService> logger)
    {
        _logger = logger;
    }

    public Task<string> CreateApprovalAsync(
        string agentId,
        string userId,
        string toolName,
        object? arguments = null)
    {
        var approvalId = Guid.NewGuid().ToString("N");

        var approval = new Models.Entities.Approval
        {
            ApprovalId = approvalId,
            AgentId = agentId,
            ToolName = toolName,
            Arguments = arguments,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            Decision = "pending"
        };

        _approvals[approvalId] = approval;
        _logger.LogInformation(
            "Created approval request: {ApprovalId} for tool: {ToolName} by user: {UserId}",
            approvalId, toolName, userId);

        return Task.FromResult(approvalId);
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

        // TODO: 触发 Agent 继续执行
        // 需要通过 EventBus 通知 Agent
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

        // TODO: 触发 Agent 停止执行
        // 需要通过 EventBus 通知 Agent
        return Task.FromResult(true);
    }
}
