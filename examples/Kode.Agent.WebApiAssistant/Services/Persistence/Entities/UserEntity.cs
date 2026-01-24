using System.ComponentModel.DataAnnotations;

namespace Kode.Agent.WebApiAssistant.Services.Persistence.Entities;

/// <summary>
/// 用户实体
/// </summary>
public class UserEntity
{
    [Key]
    [MaxLength(256)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? DisplayName { get; set; }

    [MaxLength(256)]
    public string AgentId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime LastActiveAt { get; set; }

    // 导航属性
    public List<SessionEntity> Sessions { get; set; } = new();
}
