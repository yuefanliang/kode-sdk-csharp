namespace Kode.Agent.WebApiAssistant.Models.Entities;

/// <summary>
/// 工具操作类型
/// </summary>
public enum ToolOperationType
{
    /// <summary>读取操作</summary>
    Read = 1,
    /// <summary>写入操作</summary>
    Write = 2,
    /// <summary>删除操作（敏感）</summary>
    Delete = 3,
    /// <summary>执行操作（敏感）</summary>
    Execute = 4,
    /// <summary>其他操作</summary>
    Other = 0
}

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

    /// <summary>操作类型</summary>
    public ToolOperationType OperationType { get; set; } = ToolOperationType.Other;

    /// <summary>是否为敏感操作</summary>
    public bool IsSensitive { get; set; } = false;

    /// <summary>关联的调用ID</summary>
    public string? CallId { get; set; }

    /// <summary>会话ID</summary>
    public string? SessionId { get; set; }
}
