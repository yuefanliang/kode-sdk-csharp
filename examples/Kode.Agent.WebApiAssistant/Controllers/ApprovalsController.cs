using Kode.Agent.WebApiAssistant.Models.Requests;
using Kode.Agent.WebApiAssistant.Models.Responses;
using Kode.Agent.WebApiAssistant.Services;
using Microsoft.AspNetCore.Mvc;

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
}
