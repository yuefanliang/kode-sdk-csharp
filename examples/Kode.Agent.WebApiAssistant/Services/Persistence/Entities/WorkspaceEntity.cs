using System.ComponentModel.DataAnnotations;

namespace Kode.Agent.WebApiAssistant.Services.Persistence.Entities;

/// <summary>
/// 工作区实体
/// </summary>
public class WorkspaceEntity
{
    [Key]
    [MaxLength(256)]
    public string WorkspaceId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Description { get; set; }

    [MaxLength(1024)]
    public string? WorkDir { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsActive { get; set; } = false;
}
