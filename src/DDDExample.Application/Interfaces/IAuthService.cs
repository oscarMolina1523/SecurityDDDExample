using DDDExample.Application.DTOs;
namespace DDDExample.Application.Interfaces;

public interface IAuthService
{
    Task RevokeUserTokensAsync(Guid userId);
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);
    Task<LoginResponse> VerifyMfaAsync(MfaVerifyRequest request);
    Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task<MfaSetupResponse> SetupMfaAsync(Guid userId, MfaSetupRequest request);
    Task<bool> EnableMfaAsync(Guid userId, string code);
    Task<bool> DisableMfaAsync(Guid userId);
}