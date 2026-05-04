using DDDExample.Infrastructure.Persistence.SqlServer;
using DDDExample.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DDDExample.Application.DTOs;
using Microsoft.AspNetCore.Identity;
using DDDExample.Application.Interfaces;
using DDDExample.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using OtpNet;
using QRCoder;
using Microsoft.Extensions.Logging;


namespace DDDExample.Infrastructure.Services;


public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly byte[] _mfaTokenKey = Encoding.UTF8.GetBytes("THIS_IS_MY_SUPER_SECURE_TEMP_MFA_SECRET_KEY_2026_123456");

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        ApplicationDbContext context, ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _context = context;
        _logger = logger;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return new LoginResponse { RequiresMfa = false };
        }

        user.UpdateLastLogin();
        await _userManager.UpdateAsync(user);

        // Si MFA está habilitado, requerir segundo factor
        if (user.MfaEnabled)
        {
            var mfaToken = GenerateMfaToken(user);
            return new LoginResponse
            {
                RequiresMfa = true,
                MfaToken = mfaToken,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    FullName = user.FullName,
                    CreatedAt = user.CreatedAt,
                    MfaEnabled = user.MfaEnabled
                }
            };
        }

        // Generar tokens si no hay MFA
        var jwtToken = _tokenService.GenerateToken(user);
        var refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id);

        return new LoginResponse
        {
            Token = jwtToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                CreatedAt = user.CreatedAt,
                MfaEnabled = user.MfaEnabled
            }
        };
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return new RegisterResponse
            {
                Success = false,
                Message = "Email already exists"
            };
        }

        var user = new ApplicationUser(request.Email, request.FirstName, request.LastName);

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return new RegisterResponse
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            };
        }

        return new RegisterResponse
        {
            Success = true,
            Message = "User registered successfully",
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                CreatedAt = user.CreatedAt
            }
        };
    }

    public async Task<MfaSetupResponse> SetupMfaAsync(Guid userId, MfaSetupRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
            throw new Exception("User not found");

        var validPassword = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!validPassword)
            throw new Exception("Invalid password");

        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretBytes);

        var backupCodes = Enumerable.Range(0, 8)
            .Select(_ => Guid.NewGuid().ToString("N")[..8].ToUpper())
            .ToList();

        user.MfaSecret = secret;
        user.BackupCodes = JsonSerializer.Serialize(backupCodes);

        await _userManager.UpdateAsync(user);

        var issuer = "DDDExample";
        var otpUri = $"otpauth://totp/{issuer}:{user.Email}?secret={secret}&issuer={issuer}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(otpUri, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrBytes = qrCode.GetGraphic(20);

        return new MfaSetupResponse
        {
            Secret = secret,
            ManualEntryKey = secret,
            BackupCodes = backupCodes,
            QrCode = Convert.ToBase64String(qrBytes)
        };
    }

    private string GenerateMfaToken(ApplicationUser user)
    {
        // Generar token temporal para MFA (válido por 5 minutos)
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("mfa-challenge", "true")
            }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_mfaTokenKey), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private Guid? ValidateMfaToken(string mfaToken)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            tokenHandler.ValidateToken(mfaToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_mfaTokenKey),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;

            var userIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier ||
                c.Type == "nameid" ||
                c.Type == JwtRegisteredClaimNames.Sub
            );

            if (userIdClaim == null)
            {
                _logger.LogError("No user identifier claim found in MFA token.");
                return null;
            }

            _logger.LogInformation("Found user claim type: {Type}", userIdClaim.Type);

            return Guid.Parse(userIdClaim.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MFA token validation exception.");
            return null;
        }
    }

    private bool VerifyBackupCode(ApplicationUser user, string code)
    {
        if (string.IsNullOrEmpty(user.BackupCodes))
            return false;

        var codes = JsonSerializer.Deserialize<List<string>>(user.BackupCodes);
        if (codes == null || !codes.Contains(code))
            return false;

        // Remover código usado
        codes.Remove(code);
        user.BackupCodes = JsonSerializer.Serialize(codes);
        _userManager.UpdateAsync(user);

        return true;
    }

    private async Task RememberDeviceAsync(Guid userId)
    {
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceFingerprint = GenerateDeviceFingerprint(),
            RememberMfaUntil = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            LastAccessAt = DateTime.UtcNow
        }; 

        _context.UserSessions.Add(session);
        await _context.SaveChangesAsync();
    }

    private string GenerateDeviceFingerprint()
    {
        // Implementar lógica para generar fingerprint de dispositivo
        return Guid.NewGuid().ToString();
    }

    public async Task RevokeUserTokensAsync(Guid userId)
    {
        await _refreshTokenService.RevokeAllUserTokensAsync(userId);
    }

    // REEMPLAZA VerifyMfaAsync COMPLETO POR ESTA VERSIÓN COMPATIBLE CON TU DTO ACTUAL
    public async Task<LoginResponse> VerifyMfaAsync(MfaVerifyRequest request)
    {
        _logger.LogInformation("===== MFA VERIFY START =====");
        _logger.LogInformation("Incoming MFA Token: {Token}", request.MfaToken);
        _logger.LogInformation("Incoming MFA Code Raw: {Code}", request.Code);

        var userId = ValidateMfaToken(request.MfaToken);

        if (!userId.HasValue)
        {
            _logger.LogWarning("MFA token validation failed.");
            return new LoginResponse { RequiresMfa = false };
        }

        _logger.LogInformation("Validated UserId from MFA Token: {UserId}", userId.Value);

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());

        if (user == null)
        {
            _logger.LogWarning("User not found.");
            return new LoginResponse { RequiresMfa = false };
        }

        _logger.LogInformation("User found: {Email}", user.Email);
        _logger.LogInformation("MfaEnabled: {Enabled}", user.MfaEnabled);
        _logger.LogInformation("Stored Secret: {Secret}", user.MfaSecret);

        if (string.IsNullOrEmpty(user.MfaSecret))
        {
            _logger.LogWarning("User has no MFA secret.");
            return new LoginResponse { RequiresMfa = false };
        }

        var cleanedCode = request.Code.Replace(" ", "").Trim();

        _logger.LogInformation("Cleaned MFA Code: {Code}", cleanedCode);
        _logger.LogInformation("Server UTC Time: {UtcNow}", DateTime.UtcNow);

        try
        {
            var secretBytes = Base32Encoding.ToBytes(user.MfaSecret);

            _logger.LogInformation("Secret bytes length: {Length}", secretBytes.Length);

            var totp = new Totp(secretBytes);

            var currentCode = totp.ComputeTotp(DateTime.UtcNow);
            var prevCode = totp.ComputeTotp(DateTime.UtcNow.AddSeconds(-30));
            var nextCode = totp.ComputeTotp(DateTime.UtcNow.AddSeconds(30));

            _logger.LogInformation("Expected Current TOTP: {Current}", currentCode);
            _logger.LogInformation("Expected Previous TOTP: {Previous}", prevCode);
            _logger.LogInformation("Expected Next TOTP: {Next}", nextCode);

            var isValid = totp.VerifyTotp(
                cleanedCode,
                out long matchedStep,
                new VerificationWindow(previous: 5, future: 5)
            );

            _logger.LogInformation("TOTP Validation Result: {Valid}", isValid);
            _logger.LogInformation("Matched Time Step: {Step}", matchedStep);

            if (!isValid)
            {
                _logger.LogWarning("MFA code invalid.");
                return new LoginResponse
                {
                    RequiresMfa = false
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during MFA verification.");
            return new LoginResponse
            {
                RequiresMfa = false
            };
        }

        if (request.RememberDevice)
        {
            _logger.LogInformation("Remember device enabled.");
            await RememberDeviceAsync(user.Id);
        }

        var jwtToken = _tokenService.GenerateToken(user);
        var refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id);

        _logger.LogInformation("MFA verification successful.");
        _logger.LogInformation("===== MFA VERIFY END =====");

        return new LoginResponse
        {
            Token = jwtToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            RequiresMfa = false,
            MfaToken = null,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                CreatedAt = user.CreatedAt,
                MfaEnabled = user.MfaEnabled
            }
        };
    }

    // REEMPLAZA SOLO TU MÉTODO RefreshTokenAsync POR ESTE
    // Compatible con tu DTO real y tus interfaces actuales

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        // Validar refresh token (tu servicio devuelve bool)
        var isValid = await _refreshTokenService.ValidateRefreshTokenAsync(request.RefreshToken);

        if (!isValid)
        {
            return new LoginResponse
            {
                RequiresMfa = false
            };
        }

        // Buscar el refresh token real en base de datos
        var storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt =>
                rt.Token == request.RefreshToken &&
                !rt.IsRevoked &&
                rt.ExpiresAt > DateTime.UtcNow
            );

        if (storedToken == null || storedToken.User == null)
        {
            return new LoginResponse
            {
                RequiresMfa = false
            };
        }

        var user = storedToken.User;

        // Revocar tokens anteriores
        await _refreshTokenService.RevokeAllUserTokensAsync(user.Id);

        // Generar nuevos tokens
        var newJwtToken = _tokenService.GenerateToken(user);
        var newRefreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id);

        return new LoginResponse
        {
            Token = newJwtToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            RequiresMfa = false,
            MfaToken = null,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                CreatedAt = user.CreatedAt,
                MfaEnabled = user.MfaEnabled
            }
        };
    }

    public async Task<bool> EnableMfaAsync(Guid userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null || string.IsNullOrEmpty(user.MfaSecret))
            return false;

        var totp = new Totp(Base32Encoding.ToBytes(user.MfaSecret));

        var isValid = totp.VerifyTotp(
            code,
            out _,
            new VerificationWindow(previous: 2, future: 2)
        );

        if (!isValid)
            return false;

        user.MfaEnabled = true;
        user.MfaSetupCompleted = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        return result.Succeeded;
    }

    public async Task<bool> DisableMfaAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
            return false;

        user.MfaEnabled = false;
        user.MfaSecret = null;
        user.BackupCodes = null;
        user.MfaSetupCompleted = null;

        var result = await _userManager.UpdateAsync(user);

        return result.Succeeded;
    }
}