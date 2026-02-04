namespace Kode.Agent.WebApiAssistant.Models.Responses;

/// <summary>
/// 审批事项响应模型
/// </summary>
public record ApprovalResponse
{
    public required string ApprovalId { get; init; }
    public required string AgentId { get; init; }
    public string? SessionId { get; init; }
    public required string ToolName { get; init; }
    public required object? Arguments { get; init; }
    public required string UserId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime? DecidedAt { get; init; }
    public required string Decision { get; init; }
    public string? DecidedBy { get; init; }
    public string? Note { get; init; }
    public bool IsSensitive { get; init; }
    public Models.Entities.ToolOperationType OperationType { get; init; }
    public string? CallId { get; init; }

    public static ApprovalResponse FromEntity(Models.Entities.Approval entity)
    {
        return new ApprovalResponse
        {
            ApprovalId = entity.ApprovalId,
            AgentId = entity.AgentId,
            SessionId = entity.SessionId,
            ToolName = entity.ToolName,
            Arguments = entity.Arguments,
            UserId = entity.UserId,
            CreatedAt = entity.CreatedAt,
            DecidedAt = entity.DecidedAt,
            Decision = entity.Decision,
            DecidedBy = entity.DecidedBy,
            Note = entity.Note,
            IsSensitive = entity.IsSensitive,
            OperationType = entity.OperationType,
            CallId = entity.CallId
        };
    }
}
