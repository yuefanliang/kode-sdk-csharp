namespace Kode.Agent.WebApiAssistant.Models.Requests;

/// <summary>
/// 创建工作区请求模型
/// </summary>
public record WorkspaceCreateRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? WorkDir { get; init; }
}
