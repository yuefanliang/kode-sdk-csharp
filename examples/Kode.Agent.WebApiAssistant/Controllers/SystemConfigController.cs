using Kode.Agent.WebApiAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kode.Agent.WebApiAssistant.Controllers;

/// <summary>
/// 系统配置管理API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SystemConfigController : ControllerBase
{
    private readonly SystemConfigService _configService;
    private readonly ILogger<SystemConfigController> _logger;

    public SystemConfigController(
        SystemConfigService configService,
        ILogger<SystemConfigController> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有配置分组
    /// </summary>
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups()
    {
        var groups = await _configService.GetConfigGroupsAsync();
        return Ok(groups);
    }

    /// <summary>
    /// 获取所有配置
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllConfigs()
    {
        var configs = await _configService.GetAllConfigsAsync();
        return Ok(configs.Select(c => new
        {
            c.Id,
            c.ConfigKey,
            c.ConfigValue,
            c.Group,
            c.DisplayName,
            c.Description,
            c.ValueType,
            c.IsEncrypted,
            c.IsEditable,
            c.Options,
            c.SortOrder
        }));
    }

    /// <summary>
    /// 按分组获取配置
    /// </summary>
    [HttpGet("group/{group}")]
    public async Task<IActionResult> GetConfigsByGroup(string group)
    {
        var configs = await _configService.GetConfigsByGroupAsync(group);
        return Ok(configs.Select(c => new
        {
            c.Id,
            c.ConfigKey,
            c.ConfigValue,
            c.Group,
            c.DisplayName,
            c.Description,
            c.ValueType,
            c.IsEncrypted,
            c.IsEditable,
            c.Options,
            c.SortOrder
        }));
    }

    /// <summary>
    /// 获取单个配置值
    /// </summary>
    [HttpGet("{key}")]
    public async Task<IActionResult> GetConfig(string key)
    {
        var value = await _configService.GetConfigAsync(key);
        if (value == null)
        {
            return NotFound(new { message = $"Config '{key}' not found" });
        }
        return Ok(new { key, value });
    }

    /// <summary>
    /// 更新单个配置
    /// </summary>
    [HttpPut("{key}")]
    public async Task<IActionResult> UpdateConfig(string key, [FromBody] UpdateConfigRequest request)
    {
        var success = await _configService.UpdateConfigAsync(key, request.Value);
        if (!success)
        {
            return BadRequest(new { message = $"Failed to update config '{key}'" });
        }
        return Ok(new { message = "Config updated successfully" });
    }

    /// <summary>
    /// 批量更新配置
    /// </summary>
    [HttpPut("batch")]
    public async Task<IActionResult> UpdateConfigs([FromBody] Dictionary<string, string?> configs)
    {
        var success = await _configService.UpdateConfigsAsync(configs);
        if (!success)
        {
            return BadRequest(new { message = "Failed to update configs" });
        }
        return Ok(new { message = "Configs updated successfully" });
    }

    /// <summary>
    /// 重新初始化默认配置
    /// </summary>
    [HttpPost("initialize")]
    public async Task<IActionResult> InitializeDefaults()
    {
        await _configService.InitializeDefaultConfigsAsync();
        return Ok(new { message = "Default configs initialized" });
    }
}

public class UpdateConfigRequest
{
    public string? Value { get; set; }
}
