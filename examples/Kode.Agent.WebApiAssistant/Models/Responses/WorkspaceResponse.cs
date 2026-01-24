namespace Kode.Agent.WebApiAssistant.Models.Responses;

/// <summary>
/// 工作区响应模型
/// </summary>
public record WorkspaceResponse
{
    public required string WorkspaceId { get; init; }
    public required string UserId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? WorkDir { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required bool IsActive { get; init; }

    public static WorkspaceResponse FromEntity(Models.Entities.Workspace entity)
    {
        return new WorkspaceResponse
        {
            WorkspaceId = entity.WorkspaceId,
            UserId = entity.UserId,
            Name = entity.Name,
            Description = entity.Description,
            WorkDir = entity.WorkDir,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            IsActive = entity.IsActive
        };
    }
}
