namespace DDDExample.Application.Interfaces;

public interface IMfaService
{
    string GenerateSecret();
    string GenerateQrCode(string secret, string userEmail, string issuer);
    bool VerifyToken(string secret, string token);
    List<string> GenerateBackupCodes();
}