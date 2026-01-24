using Kode.Agent.WebApiAssistant.Models.Requests;
using Kode.Agent.WebApiAssistant.Models.Responses;
using Kode.Agent.WebApiAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kode.Agent.WebApiAssistant.Controllers;

/// <summary>
/// 工作区管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ILogger<WorkspacesController> _logger;

    public WorkspacesController(
        IWorkspaceService workspaceService,
        ILogger<WorkspacesController> logger)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    /// <summary>
    /// 获取用户的所有工作区
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>工作区列表</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspaceResponse>>> ListWorkspaces(
        [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        var workspaces = await _workspaceService.ListWorkspacesAsync(userId);
        var response = workspaces.Select(WorkspaceResponse.FromEntity).ToList();

        return Ok(response);
    }

    /// <summary>
    /// 获取指定工作区详情
    /// </summary>
    /// <param name="workspaceId">工作区 ID</param>
    /// <returns>工作区详情</returns>
    [HttpGet("{workspaceId}")]
    public async Task<ActionResult<WorkspaceResponse>> GetWorkspace(string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return BadRequest(new { error = "workspaceId is required" });
        }

        var workspace = await _workspaceService.GetWorkspaceAsync(workspaceId);
        if (workspace == null)
        {
            return NotFound(new { error = "Workspace not found" });
        }

        return Ok(WorkspaceResponse.FromEntity(workspace));
    }

    /// <summary>
    /// 创建新工作区
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="request">创建请求</param>
    /// <returns>新创建的工作区</returns>
    [HttpPost]
    public async Task<ActionResult<WorkspaceResponse>> CreateWorkspace(
        [FromQuery] string userId,
        [FromBody] WorkspaceCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        var workspace = await _workspaceService.CreateWorkspaceAsync(
            userId,
            request.Name,
            request.Description,
            request.WorkDir);

        return CreatedAtAction(
            nameof(GetWorkspace),
            new { workspaceId = workspace.WorkspaceId },
            WorkspaceResponse.FromEntity(workspace));
    }

    /// <summary>
    /// 更新工作区
    /// </summary>
    /// <param name="workspaceId">工作区 ID</param>
    /// <param name="request">更新请求</param>
    /// <returns>更新后的工作区</returns>
    [HttpPatch("{workspaceId}")]
    public async Task<ActionResult<WorkspaceResponse>> UpdateWorkspace(
        string workspaceId,
        [FromBody] WorkspaceCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return BadRequest(new { error = "workspaceId is required" });
        }

        var existingWorkspace = await _workspaceService.GetWorkspaceAsync(workspaceId);
        if (existingWorkspace == null)
        {
            return NotFound(new { error = "Workspace not found" });
        }

        var updatedWorkspace = await _workspaceService.UpdateWorkspaceAsync(
            workspaceId,
            request.Name,
            request.Description,
            request.WorkDir);

        if (updatedWorkspace == null)
        {
            return NotFound(new { error = "Workspace not found" });
        }

        return Ok(WorkspaceResponse.FromEntity(updatedWorkspace));
    }

    /// <summary>
    /// 删除工作区
    /// </summary>
    /// <param name="workspaceId">工作区 ID</param>
    /// <returns>No Content</returns>
    [HttpDelete("{workspaceId}")]
    public async Task<ActionResult> DeleteWorkspace(string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return BadRequest(new { error = "workspaceId is required" });
        }

        var existingWorkspace = await _workspaceService.GetWorkspaceAsync(workspaceId);
        if (existingWorkspace == null)
        {
            return NotFound(new { error = "Workspace not found" });
        }

        await _workspaceService.DeleteWorkspaceAsync(workspaceId);
        return NoContent();
    }

    /// <summary>
    /// 设置活动工作区
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="workspaceId">工作区 ID</param>
    /// <returns>No Content</returns>
    [HttpPost("{workspaceId}/activate")]
    public async Task<ActionResult> SetActiveWorkspace(
        [FromQuery] string userId,
        string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        var workspace = await _workspaceService.GetWorkspaceAsync(workspaceId);
        if (workspace == null)
        {
            return NotFound(new { error = "Workspace not found" });
        }

        await _workspaceService.SetActiveWorkspaceAsync(userId, workspaceId);
        return Ok(new { message = "Workspace activated successfully" });
    }

    /// <summary>
    /// 获取用户的活动工作区
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>活动工作区</returns>
    [HttpGet("active")]
    public async Task<ActionResult<WorkspaceResponse?>> GetActiveWorkspace(
        [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        var activeWorkspace = await _workspaceService.GetActiveWorkspaceAsync(userId);
        if (activeWorkspace == null)
        {
            return NotFound(new { error = "No active workspace found" });
        }

        return Ok(WorkspaceResponse.FromEntity(activeWorkspace));
    }
}
