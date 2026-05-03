namespace DDDExample.Application.DTOs;

public class DisableMfaRequest
{
    public string Password { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
}