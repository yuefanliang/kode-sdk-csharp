namespace Kode.Agent.WebApiAssistant.Models.Requests;

/// <summary>
/// 审批决策请求模型
/// </summary>
public record ApprovalDecisionRequest
{
    public string? Note { get; init; }
}
