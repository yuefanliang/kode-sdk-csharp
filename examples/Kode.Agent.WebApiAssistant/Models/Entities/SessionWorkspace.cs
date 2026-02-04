namespace Kode.Agent.WebApiAssistant.Models.Entities;

/// <summary>
/// 会话工作区配置
/// </summary>
public class SessionWorkspace
{
    /// <summary>工作区ID</summary>
    public string WorkspaceId { get; set; } = string.Empty;

    /// <summary>会话ID</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>用户ID</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>工作目录路径</summary>
    public string WorkDirectory { get; set; } = string.Empty;

    /// <summary>是否为默认工作区</summary>
    public bool IsDefault { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间</summary>
    public DateTime? UpdatedAt { get; set; }
}
