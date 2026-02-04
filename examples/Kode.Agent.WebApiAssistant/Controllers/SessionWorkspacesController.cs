using Kode.Agent.WebApiAssistant.Models.Entities;
using Kode.Agent.WebApiAssistant.Models.Requests;
using Kode.Agent.WebApiAssistant.Models.Responses;
using Kode.Agent.WebApiAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kode.Agent.WebApiAssistant.Controllers;

/// <summary>
/// 会话工作区管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SessionWorkspacesController : ControllerBase
{
    private readonly ISessionWorkspaceService _sessionWorkspaceService;
    private readonly ILogger<SessionWorkspacesController> _logger;

    public SessionWorkspacesController(
        ISessionWorkspaceService sessionWorkspaceService,
        ILogger<SessionWorkspacesController> logger)
    {
        _sessionWorkspaceService = sessionWorkspaceService;
        _logger = logger;
    }

    /// <summary>
    /// 获取会话的工作区配置
    /// </summary>
    [HttpGet("sessions/{sessionId}")]
    public async Task<ActionResult<SessionWorkspaceResponse>> GetSessionWorkspace(
        string sessionId,
        [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { error = "sessionId is required" });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        var workspace = await _sessionWorkspaceService.GetSessionWorkspaceAsync(sessionId, userId);
        if (workspace == null)
        {
            return NotFound(new { error = "Session workspace not found" });
        }

        return Ok(SessionWorkspaceResponse.FromEntity(workspace));
    }

    /// <summary>
    /// 设置会话的工作区
    /// </summary>
    [HttpPut("sessions/{sessionId}")]
    public async Task<ActionResult<SessionWorkspaceResponse>> SetSessionWorkspace(
        string sessionId,
        [FromQuery] string userId,
        [FromBody] SessionWorkspaceRequest request)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { error = "sessionId is required" });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        // 验证工作目录
        var (isValid, error) = await _sessionWorkspaceService.ValidateWorkDirectoryAsync(request.WorkDirectory, userId);
        if (!isValid)
        {
            return BadRequest(new { error = error });
        }

        var workspace = await _sessionWorkspaceService.SetSessionWorkspaceAsync(
            sessionId, userId, request.WorkDirectory);

        return Ok(SessionWorkspaceResponse.FromEntity(workspace));
    }

    /// <summary>
    /// 获取用户的所有会话工作区
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SessionWorkspaceResponse>>> GetUserWorkspaces(
        [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        var workspaces = await _sessionWorkspaceService.GetUserWorkspacesAsync(userId);
        var response = workspaces.Select(SessionWorkspaceResponse.FromEntity).ToList();

        return Ok(response);
    }

    /// <summary>
    /// 删除会话工作区
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    public async Task<ActionResult> DeleteSessionWorkspace(
        string sessionId,
        [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { error = "sessionId is required" });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        var deleted = await _sessionWorkspaceService.DeleteSessionWorkspaceAsync(sessionId, userId);

        if (!deleted)
        {
            return NotFound(new { error = "Session workspace not found" });
        }

        return Ok(new { message = "Session workspace deleted successfully" });
    }
}
