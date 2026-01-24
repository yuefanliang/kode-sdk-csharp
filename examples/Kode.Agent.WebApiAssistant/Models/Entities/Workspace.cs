namespace Kode.Agent.WebApiAssistant.Models.Entities;

/// <summary>
/// 工作区实体
/// </summary>
public class Workspace
{
    public string WorkspaceId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? WorkDir { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsActive { get; set; }
}
