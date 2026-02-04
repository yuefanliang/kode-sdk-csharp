using System.ComponentModel.DataAnnotations;

namespace Kode.Agent.WebApiAssistant.Services.Persistence.Entities;

/// <summary>
/// 会话消息实体
/// </summary>
public class MessageEntity
{
    [Key]
    public string MessageId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string SessionId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string UserId { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty; // user, assistant, system

    [MaxLength(10)]
    public string Type { get; set; } = "text"; // text, tool, tool_result, approval, etc.

    public string Content { get; set; } = string.Empty;

    public string? ToolName { get; set; }

    public string? ToolArgs { get; set; }

    public string? ToolResult { get; set; }

    public string? ApprovalId { get; set; }

    public bool? ApprovalDecision { get; set; } // true=approved, false=rejected

    public string? ApprovalNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性（可选）
    public SessionEntity? Session { get; set; }
}
