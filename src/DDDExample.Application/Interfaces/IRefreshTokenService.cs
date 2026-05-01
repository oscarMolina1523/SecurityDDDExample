namespace DDDExample.Application.Interfaces;

public interface IRefreshTokenService
{
    Task<string> GenerateRefreshTokenAsync(Guid userId);
    Task<bool> ValidateRefreshTokenAsync(string token);
    Task<string> RotateRefreshTokenAsync(string currentToken);
    Task RevokeUserTokensAsync(Guid userId);
    Task RevokeAllUserTokensAsync(Guid userId);
}