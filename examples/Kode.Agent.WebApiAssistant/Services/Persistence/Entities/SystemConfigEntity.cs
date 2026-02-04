using System.ComponentModel.DataAnnotations;

namespace Kode.Agent.WebApiAssistant.Services.Persistence.Entities;

/// <summary>
/// 系统配置实体 - 存储所有系统配置项
/// </summary>
public class SystemConfigEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// 配置键（唯一）
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>
    /// 配置值
    /// </summary>
    public string? ConfigValue { get; set; }

    /// <summary>
    /// 配置分组（用于前端分类展示）
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Group { get; set; } = "General";

    /// <summary>
    /// 配置显示名称
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 配置描述
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 配置值类型（string, int, bool, json, list等）
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string ValueType { get; set; } = "string";

    /// <summary>
    /// 是否加密存储（用于敏感信息如API密钥）
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// 是否允许在UI中编辑
    /// </summary>
    public bool IsEditable { get; set; } = true;

    /// <summary>
    /// 可选值列表（JSON格式，用于下拉选择）
    /// </summary>
    public string? Options { get; set; }

    /// <summary>
    /// 配置排序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
