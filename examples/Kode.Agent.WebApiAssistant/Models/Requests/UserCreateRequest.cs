using System.Text.Json.Serialization;

namespace Kode.Agent.WebApiAssistant.Models.Requests;

/// <summary>
/// 用户创建请求（指定用户ID）
/// </summary>
public class UserCreateRequest
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}
