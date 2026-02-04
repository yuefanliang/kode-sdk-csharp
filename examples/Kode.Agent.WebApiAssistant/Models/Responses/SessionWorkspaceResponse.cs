namespace Kode.Agent.WebApiAssistant.Models.Responses;

/// <summary>
/// 会话工作区响应
/// </summary>
public class SessionWorkspaceResponse
{
    public string WorkspaceId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string WorkDirectory { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static SessionWorkspaceResponse FromEntity(Models.Entities.SessionWorkspace entity)
    {
        return new SessionWorkspaceResponse
        {
            WorkspaceId = entity.WorkspaceId,
            SessionId = entity.SessionId,
            WorkDirectory = entity.WorkDirectory,
            IsDefault = entity.IsDefault,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}

/// <summary>
/// 会话工作区请求
/// </summary>
public class SessionWorkspaceRequest
{
    public string WorkDirectory { get; set; } = string.Empty;
}
