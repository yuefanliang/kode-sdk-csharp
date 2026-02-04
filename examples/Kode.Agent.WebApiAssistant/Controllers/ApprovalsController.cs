using Kode.Agent.WebApiAssistant.Models.Requests;
using Kode.Agent.WebApiAssistant.Models.Responses;
using Kode.Agent.WebApiAssistant.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Kode.Agent.WebApiAssistant.Controllers;

/// <summary>
/// 审批管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ApprovalsController : ControllerBase
{
    private readonly IApprovalService _approvalService;
    private readonly ILogger<ApprovalsController> _logger;

    public ApprovalsController(
        IApprovalService approvalService,
        ILogger<ApprovalsController> logger)
    {
        _approvalService = approvalService;
        _logger = logger;
    }

    /// <summary>
    /// 获取用户的待审批列表
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>待审批列表</returns>
    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<ApprovalResponse>>> GetPendingApprovals(
        [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        var approvals = await _approvalService.GetPendingApprovalsAsync(userId);
        var response = approvals.Select(ApprovalResponse.FromEntity).ToList();

        return Ok(response);
    }

    /// <summary>
    /// 获取指定审批详情
    /// </summary>
    /// <param name="approvalId">审批 ID</param>
    /// <returns>审批详情</returns>
    [HttpGet("{approvalId}")]
    public async Task<ActionResult<ApprovalResponse>> GetApproval(string approvalId)
    {
        if (string.IsNullOrWhiteSpace(approvalId))
        {
            return BadRequest(new { error = "approvalId is required" });
        }

        var approval = await _approvalService.GetApprovalAsync(approvalId);
        if (approval == null)
        {
            return NotFound(new { error = "Approval not found" });
        }

        return Ok(ApprovalResponse.FromEntity(approval));
    }

    /// <summary>
    /// 确认审批
    /// </summary>
    /// <param name="approvalId">审批 ID</param>
    /// <param name="userId">用户 ID</param>
    /// <param name="request">决策请求</param>
    /// <returns>成功响应</returns>
    [HttpPost("{approvalId}/confirm")]
    public async Task<ActionResult> ConfirmApproval(
        string approvalId,
        [FromQuery] string userId,
        [FromBody] ApprovalDecisionRequest? request)
    {
        if (string.IsNullOrWhiteSpace(approvalId))
        {
            return BadRequest(new { error = "approvalId is required" });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        var success = await _approvalService.ConfirmApprovalAsync(
            approvalId,
            userId,
            request?.Note);

        if (!success)
        {
            return NotFound(new { error = "Approval not found or already decided" });
        }

        return Ok(new
        {
            approved = true,
            approvalId = approvalId,
            message = "Approval confirmed successfully"
        });
    }

    /// <summary>
    /// 取消审批
    /// </summary>
    /// <param name="approvalId">审批 ID</param>
    /// <param name="userId">用户 ID</param>
    /// <param name="request">决策请求</param>
    /// <returns>成功响应</returns>
    [HttpPost("{approvalId}/cancel")]
    public async Task<ActionResult> CancelApproval(
        string approvalId,
        [FromQuery] string userId,
        [FromBody] ApprovalDecisionRequest? request)
    {
        if (string.IsNullOrWhiteSpace(approvalId))
        {
            return BadRequest(new { error = "approvalId is required" });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        var success = await _approvalService.CancelApprovalAsync(
            approvalId,
            userId,
            request?.Note);

        if (!success)
        {
            return NotFound(new { error = "Approval not found or already decided" });
        }

        return Ok(new
        {
            approved = false,
            approvalId = approvalId,
            message = "Approval cancelled successfully"
        });
    }

    /// <summary>
    /// 批准工具调用
    /// </summary>
    /// <param name="approvalId">审批ID</param>
    /// <param name="request">批准请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("{approvalId}/approve")]
    public async Task<ActionResult> ApproveToolCall(
        string approvalId,
        [FromBody] JsonElement request)
    {
        if (string.IsNullOrWhiteSpace(approvalId))
        {
            return BadRequest(new { error = "approvalId is required" });
        }

        try
        {
            // 正确处理dynamic类型的note字段
            string? note = null;
            if (request.ValueKind != JsonValueKind.Undefined &&
                request.ValueKind != JsonValueKind.Null &&
                request.TryGetProperty("note", out var noteProperty) &&
                noteProperty.ValueKind != JsonValueKind.Undefined &&
                noteProperty.ValueKind != JsonValueKind.Null)
            {
                note = noteProperty.ToString();
            }

            if (string.IsNullOrWhiteSpace(note))
            {
                note = "用户批准";
            }

            // 使用简化的工具审批方法（不验证审批人）
            var success = await _approvalService.PerformToolApprovalActionAsync(
                approvalId,
                "approve",
                note);

            if (!success)
            {
                var existing = await _approvalService.GetApprovalAsync(approvalId);
                if (existing == null)
                {
                    return NotFound(new { error = "Approval not found" });
                }
                return Ok(new
                {
                    success = true,
                    message = "审批已处理",
                    decision = existing.Decision
                });
            }

            _logger.LogInformation("Tool call approved for approval {ApprovalId}", approvalId);

            return Ok(new { success = true, message = "工具调用已批准" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve tool call");
            return StatusCode(500, new { error = "Failed to approve", message = ex.Message });
        }
    }

    /// <summary>
    /// 拒绝工具调用
    /// </summary>
    /// <param name="approvalId">审批ID</param>
    /// <param name="request">拒绝请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("{approvalId}/reject")]
    public async Task<ActionResult> RejectToolCall(
        string approvalId,
        [FromBody] JsonElement request)
    {
        if (string.IsNullOrWhiteSpace(approvalId))
        {
            return BadRequest(new { error = "approvalId is required" });
        }

        try
        {
            // 正确处理dynamic类型的reason字段
            string? reason = null;
            if (request.ValueKind != JsonValueKind.Undefined &&
                request.ValueKind != JsonValueKind.Null &&
                request.TryGetProperty("reason", out var reasonProperty) &&
                reasonProperty.ValueKind != JsonValueKind.Undefined &&
                reasonProperty.ValueKind != JsonValueKind.Null)
            {
                reason = reasonProperty.ToString();
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "用户拒绝执行";
            }

            // 使用简化的工具审批方法（不验证审批人）
            var success = await _approvalService.PerformToolApprovalActionAsync(
                approvalId,
                "reject",
                reason);

            if (!success)
            {
                var existing = await _approvalService.GetApprovalAsync(approvalId);
                if (existing == null)
                {
                    return NotFound(new { error = "Approval not found" });
                }
                return Ok(new
                {
                    success = true,
                    message = "审批已处理",
                    decision = existing.Decision
                });
            }

            _logger.LogInformation("Tool call rejected for approval {ApprovalId}", approvalId);

            return Ok(new { success = true, message = "工具调用已拒绝" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject tool call");
            return StatusCode(500, new { error = "Failed to reject", message = ex.Message });
        }
    }
}
