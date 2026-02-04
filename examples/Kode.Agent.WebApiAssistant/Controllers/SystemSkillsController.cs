using Kode.Agent.WebApiAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kode.Agent.WebApiAssistant.Controllers;

/// <summary>
/// 系统级Skill管理API - 管理全局可用的技能包
/// </summary>
[ApiController]
[Route("api/system/skills")]
public class SystemSkillsController : ControllerBase
{
    private readonly SystemSkillService _skillService;
    private readonly ILogger<SystemSkillsController> _logger;

    public SystemSkillsController(
        SystemSkillService skillService,
        ILogger<SystemSkillsController> logger)
    {
        _skillService = skillService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有系统技能
    /// </summary>
    [HttpGet]
    public IActionResult GetSkills()
    {
        try
        {
            var skills = _skillService.GetAllSkills();
            var skillPath = _skillService.GetSkillDirectory();
            
            return Ok(new
            {
                skillPath,
                skills = skills.Select(s => new
                {
                    s.Id,
                    s.SkillId,
                    s.DisplayName,
                    s.Description,
                    s.Version,
                    s.IsActive,
                    s.Source,
                    s.Path
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system skills");
            return StatusCode(500, new { message = $"Failed to get skills: {ex.Message}" });
        }
    }

    /// <summary>
    /// 扫描技能目录
    /// </summary>
    [HttpPost("scan")]
    public async Task<IActionResult> ScanSkills()
    {
        try
        {
            var skills = await _skillService.ScanSkillDirectoryAsync();
            return Ok(new
            {
                message = $"Scan completed, found {skills.Count} skills",
                skills = skills.Select(s => new
                {
                    s.Id,
                    s.SkillId,
                    s.DisplayName,
                    s.Description,
                    s.Version,
                    s.IsActive
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan skill directory");
            return BadRequest(new { message = $"Failed to scan: {ex.Message}" });
        }
    }

    /// <summary>
    /// 上传技能压缩包
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadSkill(IFormFile file, [FromForm] string skillId)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Please upload a file" });
            }

            if (string.IsNullOrWhiteSpace(skillId))
            {
                return BadRequest(new { message = "Please provide skill ID" });
            }

            // 检查文件类型
            if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only ZIP files are supported" });
            }

            var skill = await _skillService.UploadSkillAsync(skillId, file);
            return Ok(new
            {
                message = "Skill uploaded successfully",
                skill = new
                {
                    skill.Id,
                    skill.SkillId,
                    skill.DisplayName,
                    skill.Description,
                    skill.Version,
                    skill.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload skill {SkillId}", skillId);
            return BadRequest(new { message = $"Failed to upload skill: {ex.Message}" });
        }
    }

    /// <summary>
    /// 从GitHub批量导入技能
    /// </summary>
    [HttpPost("import-github")]
    public async Task<IActionResult> ImportFromGitHub([FromBody] ImportSystemSkillsRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.GitUrl))
            {
                return BadRequest(new { message = "Please provide GitHub repository URL" });
            }

            var result = await _skillService.ImportSkillsFromGitHubAsync(
                request.GitUrl, request.Branch, request.SubDir);

            return Ok(new
            {
                message = $"Imported {result.Skills.Count} skills from GitHub",
                skills = result.Skills.Select(s => new
                {
                    s.Id,
                    s.SkillId,
                    s.DisplayName,
                    s.Description,
                    s.Version,
                    s.IsActive
                }),
                failed = result.Failed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import skills from GitHub");
            return BadRequest(new { message = $"Failed to import skills: {ex.Message}" });
        }
    }

    /// <summary>
    /// 切换技能激活状态
    /// </summary>
    [HttpPost("{skillId}/toggle")]
    public IActionResult ToggleSkill(string skillId, [FromBody] ToggleSkillRequest request)
    {
        try
        {
            var success = _skillService.ToggleSkillActive(skillId, request.IsActive);
            if (!success)
            {
                return NotFound(new { message = "Skill not found" });
            }
            return Ok(new { message = request.IsActive ? "Skill activated" : "Skill deactivated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle skill {SkillId}", skillId);
            return BadRequest(new { message = $"Failed to toggle skill: {ex.Message}" });
        }
    }

    /// <summary>
    /// 删除技能
    /// </summary>
    [HttpDelete("{skillId}")]
    public IActionResult DeleteSkill(string skillId)
    {
        try
        {
            var success = _skillService.DeleteSkill(skillId);
            if (!success)
            {
                return NotFound(new { message = "Skill not found" });
            }
            return Ok(new { message = "Skill deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete skill {SkillId}", skillId);
            return BadRequest(new { message = $"Failed to delete skill: {ex.Message}" });
        }
    }
}

public class ImportSystemSkillsRequest
{
    public string GitUrl { get; set; } = string.Empty;
    public string? Branch { get; set; }
    public string? SubDir { get; set; }
}

public class ToggleSkillRequest
{
    public bool IsActive { get; set; }
}
