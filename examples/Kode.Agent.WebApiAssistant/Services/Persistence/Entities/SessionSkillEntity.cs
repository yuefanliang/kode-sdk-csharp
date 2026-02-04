using System.ComponentModel.DataAnnotations;

namespace Kode.Agent.WebApiAssistant.Services.Persistence.Entities;

/// <summary>
/// 会话级Skill配置实体 - 每个会话可以有自己的Skill配置
/// </summary>
public class SessionSkillEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// 会话ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Skill名称/ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string SkillId { get; set; } = string.Empty;

    /// <summary>
    /// Skill显示名称
    /// </summary>
    [MaxLength(200)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Skill描述
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Skill来源：builtin(内置), downloaded(下载), custom(自定义)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Source { get; set; } = "builtin";

    /// <summary>
    /// Skill本地路径
    /// </summary>
    [MaxLength(500)]
    public string? LocalPath { get; set; }

    /// <summary>
    /// Skill远程URL（如果是下载的）
    /// </summary>
    [MaxLength(500)]
    public string? RemoteUrl { get; set; }

    /// <summary>
    /// 是否已激活
    /// </summary>
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Skill版本
    /// </summary>
    [MaxLength(50)]
    public string? Version { get; set; }

    /// <summary>
    /// Skill配置参数（JSON格式）
    /// </summary>
    public string? ConfigJson { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    public virtual SessionEntity? Session { get; set; }
}
