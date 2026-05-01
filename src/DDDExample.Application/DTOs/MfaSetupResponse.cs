namespace DDDExample.Application.DTOs;

public class MfaSetupResponse
{
    public string QrCode { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public List<string> BackupCodes { get; set; } = new();
    public string ManualEntryKey { get; set; } = string.Empty;
}