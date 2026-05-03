namespace DDDExample.Application.DTOs;

public class EnableMfaRequest
{
    public string VerificationCode { get; set; } = string.Empty;
}