using Kode.Agent.WebApiAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kode.Agent.WebApiAssistant.Controllers;

/// <summary>
/// 会话级Skill管理API
/// </summary>
[ApiController]
[Route("api/sessions/{sessionId}/skills")]
public class SessionSkillsController : ControllerBase
{
    private readonly SessionSkillService _skillService;
    private readonly ILogger<SessionSkillsController> _logger;

    public SessionSkillsController(
        SessionSkillService skillService,
        ILogger<SessionSkillsController> logger)
    {
        _skillService = skillService;
        _logger = logger;
    }

    /// <summary>
    /// 上传Skill压缩包
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadSkill(string sessionId, IFormFile file, [FromForm] string skillId)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "请上传文件" });
            }

            if (string.IsNullOrWhiteSpace(skillId))
            {
                return BadRequest(new { message = "请提供技能ID" });
            }

            // 检查文件类型
            if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "只支持ZIP格式的压缩包" });
            }

            var skill = await _skillService.UploadSkillAsync(sessionId, skillId, file);
            return Ok(new
            {
                message = "Skill uploaded successfully",
                skill = new
                {
                    skill.Id,
                    skill.SkillId,
                    skill.DisplayName,
                    skill.Description,
                    skill.Source,
                    skill.IsActive,
                    skill.Version
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload skill {SkillId} for session {SessionId}",
                skillId, sessionId);
            return BadRequest(new { message = $"Failed to upload skill: {ex.Message}" });
        }
    }

    /// <summary>
    /// 从GitHub批量导入技能
    /// </summary>
    [HttpPost("import-github")]
    public async Task<IActionResult> ImportFromGitHub(string sessionId, [FromBody] ImportGitHubRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.GitUrl))
            {
                return BadRequest(new { message = "请提供GitHub仓库地址" });
            }

            var result = await _skillService.ImportSkillsFromGitHubAsync(
                sessionId, request.GitUrl, request.Branch, request.SubDir);

            return Ok(new
            {
                message = $"Imported {result.Skills.Count} skills from GitHub",
                skills = result.Skills.Select(s => new
                {
                    s.Id,
                    s.SkillId,
                    s.DisplayName,
                    s.Description,
                    s.Source,
                    s.IsActive,
                    s.Version
                }),
                failed = result.Failed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import skills from GitHub for session {SessionId}",
                sessionId);
            return BadRequest(new { message = $"Failed to import skills: {ex.Message}" });
        }
    }

    /// <summary>
    /// 获取会话的所有Skill
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSkills(string sessionId)
    {
        var skills = await _skillService.GetSessionSkillsAsync(sessionId);
        return Ok(skills.Select(s => new
        {
            s.Id,
            s.SkillId,
            s.DisplayName,
            s.Description,
            s.Source,
            s.IsActive,
            s.Version,
            s.RemoteUrl,
            s.CreatedAt,
            s.UpdatedAt
        }));
    }

    /// <summary>
    /// 获取单个Skill
    /// </summary>
    [HttpGet("{skillId}")]
    public async Task<IActionResult> GetSkill(string sessionId, string skillId)
    {
        var skill = await _skillService.GetSkillAsync(sessionId, skillId);
        if (skill == null)
        {
            return NotFound(new { message = "Skill not found" });
        }

        return Ok(new
        {
            skill.Id,
            skill.SkillId,
            skill.DisplayName,
            skill.Description,
            skill.Source,
            skill.IsActive,
            skill.Version,
            skill.RemoteUrl,
            skill.ConfigJson,
            skill.CreatedAt,
            skill.UpdatedAt
        });
    }

    /// <summary>
    /// 从URL下载Skill
    /// </summary>
    [HttpPost("download")]
    public async Task<IActionResult> DownloadSkill(string sessionId, [FromBody] DownloadSkillRequest request)
    {
        try
        {
            var skill = await _skillService.DownloadSkillAsync(sessionId, request.SkillId, request.SourceUrl);
            return Ok(new
            {
                message = "Skill downloaded successfully",
                skill = new
                {
                    skill.Id,
                    skill.SkillId,
                    skill.DisplayName,
                    skill.Description,
                    skill.Source,
                    skill.IsActive,
                    skill.Version
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download skill {SkillId} for session {SessionId}",
                request.SkillId, sessionId);
            return BadRequest(new { message = $"Failed to download skill: {ex.Message}" });
        }
    }

    /// <summary>
    /// 从Git仓库克隆Skill
    /// </summary>
    [HttpPost("clone")]
    public async Task<IActionResult> CloneSkill(string sessionId, [FromBody] CloneSkillRequest request)
    {
        try
        {
            var skill = await _skillService.CloneSkillFromGitAsync(
                sessionId, request.SkillId, request.GitUrl, request.Branch);
            return Ok(new
            {
                message = "Skill cloned successfully",
                skill = new
                {
                    skill.Id,
                    skill.SkillId,
                    skill.DisplayName,
                    skill.Description,
                    skill.Source,
                    skill.IsActive,
                    skill.Version
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clone skill {SkillId} for session {SessionId}",
                request.SkillId, sessionId);
            return BadRequest(new { message = $"Failed to clone skill: {ex.Message}" });
        }
    }

    /// <summary>
    /// 激活Skill
    /// </summary>
    [HttpPost("{skillId}/activate")]
    public async Task<IActionResult> ActivateSkill(string sessionId, string skillId)
    {
        var success = await _skillService.ToggleSkillActiveAsync(sessionId, skillId, true);
        if (!success)
        {
            return NotFound(new { message = "Skill not found" });
        }
        return Ok(new { message = "Skill activated" });
    }

    /// <summary>
    /// 停用Skill
    /// </summary>
    [HttpPost("{skillId}/deactivate")]
    public async Task<IActionResult> DeactivateSkill(string sessionId, string skillId)
    {
        var success = await _skillService.ToggleSkillActiveAsync(sessionId, skillId, false);
        if (!success)
        {
            return NotFound(new { message = "Skill not found" });
        }
        return Ok(new { message = "Skill deactivated" });
    }

    /// <summary>
    /// 删除Skill
    /// </summary>
    [HttpDelete("{skillId}")]
    public async Task<IActionResult> RemoveSkill(string sessionId, string skillId)
    {
        var success = await _skillService.RemoveSkillAsync(sessionId, skillId);
        if (!success)
        {
            return NotFound(new { message = "Skill not found" });
        }
        return Ok(new { message = "Skill removed" });
    }

    /// <summary>
    /// 更新Skill配置
    /// </summary>
    [HttpPut("{skillId}/config")]
    public async Task<IActionResult> UpdateSkillConfig(string sessionId, string skillId, [FromBody] UpdateSkillConfigRequest request)
    {
        var success = await _skillService.UpdateSkillConfigAsync(sessionId, skillId, request.ConfigJson);
        if (!success)
        {
            return NotFound(new { message = "Skill not found" });
        }
        return Ok(new { message = "Skill config updated" });
    }

    /// <summary>
    /// 获取激活的Skill路径（内部API）
    /// </summary>
    [HttpGet("active-paths")]
    public async Task<IActionResult> GetActiveSkillPaths(string sessionId)
    {
        var paths = await _skillService.GetActiveSkillPathsAsync(sessionId);
        return Ok(paths);
    }
}

public class DownloadSkillRequest
{
    public string SkillId { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
}

public class CloneSkillRequest
{
    public string SkillId { get; set; } = string.Empty;
    public string GitUrl { get; set; } = string.Empty;
    public string? Branch { get; set; }
}

public class UpdateSkillConfigRequest
{
    public string ConfigJson { get; set; } = "{}";
}

public class ImportGitHubRequest
{
    public string GitUrl { get; set; } = string.Empty;
    public string? Branch { get; set; }
    public string? SubDir { get; set; }
}
