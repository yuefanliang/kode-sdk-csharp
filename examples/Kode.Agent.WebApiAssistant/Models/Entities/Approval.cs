namespace Kode.Agent.WebApiAssistant.Models.Entities;

/// <summary>
/// 审批事项实体
/// </summary>
public class Approval
{
    public string ApprovalId { get; set; } = string.Empty;

    public string AgentId { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public object? Arguments { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string Decision { get; set; } = "pending"; // pending, approved, denied

    public string? DecidedBy { get; set; }

    public string? Note { get; set; }
}
