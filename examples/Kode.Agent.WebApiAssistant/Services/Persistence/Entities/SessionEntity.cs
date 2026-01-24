using System.ComponentModel.DataAnnotations;

namespace Kode.Agent.WebApiAssistant.Services.Persistence.Entities;

/// <summary>
/// 会话实体
/// </summary>
public class SessionEntity
{
    [Key]
    [MaxLength(256)]
    public string SessionId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(512)]
    public string Title { get; set; } = "新对话";

    [MaxLength(256)]
    public string AgentId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int MessageCount { get; set; }

    // 导航属性（可选，通过 UserId 关联）
    public UserEntity? User { get; set; }
}
