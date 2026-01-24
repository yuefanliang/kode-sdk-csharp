namespace Kode.Agent.WebApiAssistant.Models.Requests;

public class UserRegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
}
