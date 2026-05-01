namespace DDDExample.Application.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
    public string RefreshToken { get; set; } = string.Empty;
    public bool RequiresMfa { get; set; }
    public string? MfaToken { get; set; }
}
