# Guía de Implementación: JWT + Refresh Tokens + MFA (TOTP)

## Tabla de Contenidos
1. [Introducción](#introducción)
2. [Requerimientos](#requerimientos)
3. [Setup de Rama](#setup-de-rama)
4. [Implementación](#implementación)
5. [Configuración](#configuración)
6. [Testing](#testing)
7. [Merge Strategy](#merge-strategy)

## Introducción

Esta guía implementa un sistema de autenticación robusto que combina JWT con refresh tokens para sesiones prolongadas y autenticación de dos factores (MFA) usando TOTP, proporcionando seguridad enterprise-grade para aplicaciones sensibles.

### Características Principales
- **Seguridad Máxima**: MFA añade capa adicional de protección
- **Sesiones Persistentes**: Refresh tokens evitan re-login frecuente
- **Experiencia Usuario**: Balance entre seguridad y usabilidad
- **Rotación Automática**: Refresh tokens se rotan para prevenir reuse
- **Offline MFA**: TOTP funciona sin conexión a internet

### Casos de Uso Ideales
- Aplicaciones bancarias y financieras
- Sistemas de salud y médicos
- Plataformas de educación online
- Aplicaciones empresariales críticas

## Requerimientos

### Prerrequisitos
- .NET 8.0 SDK
- SQL Server (como BD principal)
- MongoDB (como BD secundaria, opcional)
- Visual Studio 2022 o VS Code
- Google Authenticator (para testing TOTP)

### Dependencias NuGet
```xml
<PackageReference Include="Microsoft.AspNetCore.Identity" Version="8.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.0" />
<PackageReference Include="Microsoft.AspNetCore.DataProtection" Version="8.0.0" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.0.0" />
<PackageReference Include="OtpNet" Version="1.3.0" />
<PackageReference Include="QRCoder" Version="1.4.3" />
```

## Setup de Rama

### Crear Rama de Trabajo
```bash
# Asegurarse de estar en main
git checkout main
git pull origin main

# Crear rama para implementación avanzada
git checkout -b feature/jwt-refresh-mfa

# Push al remoto
git push -u origin feature/jwt-refresh-mfa
```

### Flujo de Trabajo
```bash
# Durante desarrollo
git add .
git commit -m "feat: implement JWT + Refresh + MFA authentication"
git push origin feature/jwt-refresh-mfa

# Para finalizar (opcional)
git checkout main
git merge feature/jwt-refresh-mfa
git push origin main
git branch -d feature/jwt-refresh-mfa
```

## Implementación

### 1. Estructura de Archivos

```
src/
DDDExample.API/
  Authentication/
    JwtMfa/
      Controllers/
        AuthController.cs
        MfaController.cs
      Services/
        IAuthService.cs
        AuthService.cs
        ITokenService.cs
        JwtTokenService.cs
        IMfaService.cs
        TotpMfaService.cs
        IRefreshTokenService.cs
        RefreshTokenService.cs
      DTOs/
        LoginRequest.cs
        LoginResponse.cs
        MfaSetupRequest.cs
        MfaSetupResponse.cs
        MfaVerifyRequest.cs
        RefreshTokenRequest.cs
      Middleware/
        MfaRequiredAttribute.cs
      Configuration/
        JwtSettings.cs
        MfaSettings.cs
        RefreshTokenSettings.cs
        JwtMfaExtensions.cs
      Models/
        ApplicationUser.cs
        ApplicationRole.cs
        RefreshToken.cs
        UserSession.cs
```

### 2. Entidades de Dominio

#### ApplicationUser.cs
```csharp
using Microsoft.AspNetCore.Identity;

namespace DDDExample.API.Authentication.JwtMfa.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    // Propiedades MFA
    public bool MfaEnabled { get; set; }
    public string? MfaSecret { get; set; }
    public DateTime? MfaSetupCompleted { get; set; }
    public string? BackupCodes { get; set; } // JSON array
    
    public string FullName => $"{FirstName} {LastName}";
    
    // Navigation properties
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public virtual ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();
}
```

#### RefreshToken.cs
```csharp
namespace DDDExample.API.Authentication.JwtMfa.Models;

public class RefreshToken
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    
    public ApplicationUser User { get; set; } = null!;
    
    public void MarkAsUsed()
    {
        IsUsed = true;
    }
    
    public void Revoke()
    {
        IsRevoked = true;
    }
    
    public void ReplaceWith(Guid newTokenId)
    {
        MarkAsUsed();
        ReplacedByTokenId = newTokenId;
    }
}
```

#### UserSession.cs
```csharp
namespace DDDExample.API.Authentication.JwtMfa.Models;

public class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public DateTime? RememberMfaUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessAt { get; set; }
    
    public ApplicationUser User { get; set; } = null!;
}
```

### 3. Configuración

#### JwtSettings.cs
```csharp
namespace DDDExample.API.Authentication.JwtMfa.Configuration;

public class JwtSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 15; // Más corto para mayor seguridad
}
```

#### MfaSettings.cs
```csharp
namespace DDDExample.API.Authentication.JwtMfa.Configuration;

public class MfaSettings
{
    public string Issuer { get; set; } = "DDDExample";
    public int Digits { get; set; } = 6;
    public int Period { get; set; } = 30;
    public int Window { get; set; } = 1;
    public int BackupCodesCount { get; set; } = 10;
    public int RememberDeviceDays { get; set; } = 30;
}
```

#### RefreshTokenSettings.cs
```csharp
namespace DDDExample.API.Authentication.JwtMfa.Configuration;

public class RefreshTokenSettings
{
    public int RefreshTokenExpirationDays { get; set; } = 30;
    public bool RotationEnabled { get; set; } = true;
    public int MaxActiveTokens { get; set; } = 5;
    public int CleanupIntervalHours { get; set; } = 24;
}
```

### 4. DTOs

#### MfaSetupRequest.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace DDDExample.API.Authentication.JwtMfa.DTOs;

public class MfaSetupRequest
{
    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}
```

#### MfaSetupResponse.cs
```csharp
namespace DDDExample.API.Authentication.JwtMfa.DTOs;

public class MfaSetupResponse
{
    public string QrCode { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public List<string> BackupCodes { get; set; } = new();
    public string ManualEntryKey { get; set; } = string.Empty;
}
```

#### MfaVerifyRequest.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace DDDExample.API.Authentication.JwtMfa.DTOs;

public class MfaVerifyRequest
{
    [Required(ErrorMessage = "Code is required")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits")]
    public string Code { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "MFA token is required")]
    public string MfaToken { get; set; } = string.Empty;
    
    public bool RememberDevice { get; set; }
}
```

#### RefreshTokenRequest.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace DDDExample.API.Authentication.JwtMfa.DTOs;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "Refresh token is required")]
    public string RefreshToken { get; set; } = string.Empty;
}
```

#### LoginResponse.cs
```csharp
namespace DDDExample.API.Authentication.JwtMfa.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
    public bool RequiresMfa { get; set; }
    public string? MfaToken { get; set; }
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool MfaEnabled { get; set; }
}
```

### 5. Servicios

#### IMfaService.cs
```csharp
namespace DDDExample.API.Authentication.JwtMfa.Services;

public interface IMfaService
{
    string GenerateSecret();
    string GenerateQrCode(string secret, string userEmail, string issuer);
    bool VerifyToken(string secret, string token);
    List<string> GenerateBackupCodes();
}
```

#### TotpMfaService.cs
```csharp
using DDDExample.API.Authentication.JwtMfa.Configuration;
using OtpNet;
using QRCoder;
using System.Drawing;
using System.Text;

namespace DDDExample.API.Authentication.JwtMfa.Services;

public class TotpMfaService : IMfaService
{
    private readonly MfaSettings _settings;

    public TotpMfaService(IOptions<MfaSettings> settings)
    {
        _settings = settings.Value;
    }

    public string GenerateSecret()
    {
        return KeyGeneration.GenerateRandomKey(20);
    }

    public string GenerateQrCode(string secret, string userEmail, string issuer)
    {
        var provisionUri = new OtpUri(
            OtpType.Totp,
            secret,
            userEmail,
            issuer,
            _settings.Digits,
            _settings.Period);

        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(provisionUri.ToString(), QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(20);

        return Convert.ToBase64String(qrCodeImage);
    }

    public bool VerifyToken(string secret, string token)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.VerifyTotp(token, out _, new VerificationWindow(_settings.Window));
    }

    public List<string> GenerateBackupCodes()
    {
        var codes = new List<string>();
        for (int i = 0; i < _settings.BackupCodesCount; i++)
        {
            codes.Add(KeyGeneration.GenerateRandomKey(8).Substring(0, 8));
        }
        return codes;
    }
}
```

#### IRefreshTokenService.cs
```csharp
namespace DDDExample.API.Authentication.JwtMfa.Services;

public interface IRefreshTokenService
{
    Task<string> GenerateRefreshTokenAsync(Guid userId);
    Task<bool> ValidateRefreshTokenAsync(string token);
    Task<string> RotateRefreshTokenAsync(string currentToken);
    Task RevokeUserTokensAsync(Guid userId);
}
```

#### RefreshTokenService.cs
```csharp
using DDDExample.API.Authentication.JwtMfa.Configuration;
using DDDExample.API.Authentication.JwtMfa.Models;
using Microsoft.EntityFrameworkCore;

namespace DDDExample.API.Authentication.JwtMfa.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly ApplicationDbContext _context;
    private readonly RefreshTokenSettings _settings;

    public RefreshTokenService(ApplicationDbContext context, IOptions<RefreshTokenSettings> settings)
    {
        _context = context;
        _settings = settings.Value;
    }

    public async Task<string> GenerateRefreshTokenAsync(Guid userId)
    {
        // Revocar tokens existentes del usuario
        await RevokeUserTokensAsync(userId);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = GenerateSecureToken(),
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return refreshToken.Token;
    }

    public async Task<bool> ValidateRefreshTokenAsync(string token)
    {
        var storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token);

        if (storedToken == null || storedToken.IsUsed || storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            return false;
        }

        return true;
    }

    public async Task<string> RotateRefreshTokenAsync(string currentToken)
    {
        var storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == currentToken);

        if (storedToken == null || storedToken.IsUsed || storedToken.IsRevoked)
        {
            throw new SecurityException("Invalid refresh token");
        }

        // Generar nuevo token
        var newToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = GenerateSecureToken(),
            UserId = storedToken.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow
        };

        // Marcar token anterior como usado
        storedToken.ReplaceWith(newToken.Id);

        _context.RefreshTokens.Add(newToken);
        await _context.SaveChangesAsync();

        return newToken.Token;
    }

    public async Task RevokeUserTokensAsync(Guid userId)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsUsed && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.Revoke();
        }

        await _context.SaveChangesAsync();
    }

    private string GenerateSecureToken()
    {
        var randomNumber = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
```

#### AuthService.cs (Extendido)
```csharp
using DDDExample.API.Authentication.JwtMfa.DTOs;
using DDDExample.API.Authentication.JwtMfa.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace DDDExample.API.Authentication.JwtMfa.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ApplicationDbContext _context;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _context = context;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return new LoginResponse { RequiresMfa = false };
        }

        user.LastLoginAt = DateTime.UtcNow;
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

    public async Task<LoginResponse> VerifyMfaAsync(MfaVerifyRequest request)
    {
        var userId = ValidateMfaToken(request.MfaToken);
        if (userId == null)
        {
            return new LoginResponse { RequiresMfa = false };
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || !user.MfaEnabled)
        {
            return new LoginResponse { RequiresMfa = false };
        }

        // Verificar código TOTP o backup code
        bool isValid = false;
        if (request.Code.Length == 6)
        {
            var mfaService = new TotpMfaService(Options.Create(new MfaSettings()));
            isValid = mfaService.VerifyToken(user.MfaSecret!, request.Code);
        }
        else
        {
            isValid = VerifyBackupCode(user, request.Code);
        }

        if (!isValid)
        {
            return new LoginResponse { RequiresMfa = false };
        }

        // Generar tokens
        var jwtToken = _tokenService.GenerateToken(user);
        var refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id);

        // Remember device si se solicita
        if (request.RememberDevice)
        {
            await RememberDeviceAsync(user.Id);
        }

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

    private string GenerateMfaToken(ApplicationUser user)
    {
        // Generar token temporal para MFA (válido por 5 minutos)
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCIIBytes("temporary-mfa-secret-key-32-chars");
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("mfa-challenge", "true")
            }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private Guid? ValidateMfaToken(string mfaToken)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCIIBytes("temporary-mfa-secret-key-32-chars");
            tokenHandler.ValidateToken(mfaToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = jwtToken.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;
            return Guid.Parse(userId);
        }
        catch
        {
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
}
```

### 6. Controllers

#### AuthController.cs
```csharp
using DDDExample.API.Authentication.JwtMfa.DTOs;
using DDDExample.API.Authentication.JwtMfa.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DDDExample.API.Authentication.JwtMfa.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        
        if (result.RequiresMfa)
        {
            return Ok(new LoginResponse
            {
                RequiresMfa = true,
                MfaToken = result.MfaToken,
                User = result.User
            });
        }

        if (string.IsNullOrEmpty(result.Token))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        return Ok(result);
    }

    [HttpPost("verify-mfa")]
    public async Task<ActionResult<LoginResponse>> VerifyMfa([FromBody] MfaVerifyRequest request)
    {
        var result = await _authService.VerifyMfaAsync(request);
        
        if (string.IsNullOrEmpty(result.Token))
        {
            return BadRequest(new { message = "Invalid MFA code" });
        }

        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);
        
        if (string.IsNullOrEmpty(result.Token))
        {
            return BadRequest(new { message = "Invalid refresh token" });
        }

        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        await _authService.RevokeUserTokensAsync(Guid.Parse(userId));
        return Ok(new { message = "Logged out successfully" });
    }
}
```

#### MfaController.cs
```csharp
using DDDExample.API.Authentication.JwtMfa.DTOs;
using DDDExample.API.Authentication.JwtMfa.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DDDExample.API.Authentication.JwtMfa.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;
    private readonly UserManager<ApplicationUser> _userManager;

    public MfaController(IMfaService mfaService, UserManager<ApplicationUser> userManager)
    {
        _mfaService = mfaService;
        _userManager = userManager;
    }

    [HttpPost("setup")]
    public async Task<ActionResult<MfaSetupResponse>> SetupMfa([FromBody] MfaSetupRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var user = await _userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            return Unauthorized();
        }

        // Verificar contraseña actual
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return BadRequest(new { message = "Invalid password" });
        }

        // Generar secret y QR code
        var secret = _mfaService.GenerateSecret();
        var qrCode = _mfaService.GenerateQrCode(secret, user.Email!, "DDDExample");
        var backupCodes = _mfaService.GenerateBackupCodes();

        // Guardar configuración temporal (no habilitada hasta verificación)
        user.MfaSecret = secret;
        user.BackupCodes = JsonSerializer.Serialize(backupCodes);
        await _userManager.UpdateAsync(user);

        return Ok(new MfaSetupResponse
        {
            QrCode = qrCode,
            Secret = secret,
            BackupCodes = backupCodes,
            ManualEntryKey = FormatManualEntryKey(secret)
        });
    }

    [HttpPost("enable")]
    public async Task<ActionResult> EnableMfa([FromBody] EnableMfaRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var user = await _userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            return Unauthorized();
        }

        if (!_mfaService.VerifyToken(user.MfaSecret!, request.VerificationCode))
        {
            return BadRequest(new { message = "Invalid verification code" });
        }

        user.MfaEnabled = true;
        user.MfaSetupCompleted = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return Ok(new { message = "MFA enabled successfully" });
    }

    [HttpPost("disable")]
    public async Task<ActionResult> DisableMfa([FromBody] DisableMfaRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var user = await _userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            return Unauthorized();
        }

        // Verificar contraseña
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return BadRequest(new { message = "Invalid password" });
        }

        // Verificar código TOTP
        if (!_mfaService.VerifyToken(user.MfaSecret!, request.VerificationCode))
        {
            return BadRequest(new { message = "Invalid verification code" });
        }

        user.MfaEnabled = false;
        user.MfaSecret = null;
        user.BackupCodes = null;
        user.MfaSetupCompleted = null;
        await _userManager.UpdateAsync(user);

        return Ok(new { message = "MFA disabled successfully" });
    }

    private string FormatManualEntryKey(string secret)
    {
        // Formatear secret para entrada manual en grupos de 4 caracteres
        return string.Join(" ", secret.Chunk(4).Select(chunk => new string(chunk.ToArray())));
    }
}

public class EnableMfaRequest
{
    public string VerificationCode { get; set; } = string.Empty;
}

public class DisableMfaRequest
{
    public string Password { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
}
```

### 7. Middleware

#### MfaRequiredAttribute.cs
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace DDDExample.API.Authentication.JwtMfa.Middleware;

public class MfaRequiredAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (!user.Identity.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var appUser = await userManager.FindByIdAsync(userId);

        if (!appUser.MfaEnabled)
        {
            context.Result = new ForbidResult();
            return;
        }

        // Verificar si MFA fue verificado en esta sesión
        var mfaVerified = context.HttpContext.Items["MfaVerified"] as bool?;
        if (!mfaVerified.HasValue || !mfaVerified.Value)
        {
            context.Result = new StatusCodeResult(428); // Precondition Required
            return;
        }

        await Task.CompletedTask;
    }
}
```

## Configuración

### 1. Program.cs

```csharp
using DDDExample.API.Authentication.JwtMfa.Configuration;
using DDDExample.API.Authentication.JwtMfa.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Data Protection para refresh tokens
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("keys"))
    .SetApplicationName("DDDExample");

// Authentication
builder.Services.AddJwtMfaAuthentication(builder.Configuration);

// Register services
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMfaService, TotpMfaService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### 2. appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=DDDExample_JwtMfa;Trusted_Connection=true;MultipleActiveResultSets=true"
  },
  "Authentication": {
    "Jwt": {
      "Issuer": "DDDExample",
      "Audience": "DDDExampleUsers",
      "SecretKey": "your-super-secret-key-256-bits-long-minimum-for-security",
      "ExpirationMinutes": 15
    },
    "Mfa": {
      "Issuer": "DDDExample",
      "Digits": 6,
      "Period": 30,
      "Window": 1,
      "BackupCodesCount": 10,
      "RememberDeviceDays": 30
    },
    "RefreshToken": {
      "RefreshTokenExpirationDays": 30,
      "RotationEnabled": true,
      "MaxActiveTokens": 5,
      "CleanupIntervalHours": 24
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

## Testing

### 1. Crear Migración

```bash
# Crear migración para MFA y refresh tokens
dotnet ef migrations add AddMfaAndRefreshTokens \
    --project src/DDDExample.API/DDDExample.API.csproj \
    --startup-project src/DDDExample.API/DDDExample.API.csproj

# Aplicar migración
dotnet ef database update \
    --project src/DDDExample.API/DDDExample.API.csproj \
    --startup-project src/DDDExample.API/DDDExample.API.csproj
```

### 2. Flujo de Testing Completo

#### Paso 1: Registro y Login Básico
```http
POST /api/auth/register
Content-Type: application/json

{
    "email": "user@example.com",
    "password": "SecurePass123",
    "firstName": "John",
    "lastName": "Doe"
}
```

#### Paso 2: Configurar MFA
```http
POST /api/mfa/setup
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
    "password": "SecurePass123"
}
```

#### Paso 3: Login con MFA Requerido
```http
POST /api/auth/login
Content-Type: application/json

{
    "email": "user@example.com",
    "password": "SecurePass123"
}
```

Response esperada:
```json
{
    "requiresMfa": true,
    "mfaToken": "abc123...",
    "user": {
        "id": "...",
        "email": "user@example.com",
        "mfaEnabled": false
    }
}
```

#### Paso 4: Verificar MFA
```http
POST /api/auth/verify-mfa
Content-Type: application/json

{
    "code": "123456",
    "mfaToken": "abc123...",
    "rememberDevice": true
}
```

#### Paso 5: Refresh Token
```http
POST /api/auth/refresh
Content-Type: application/json

{
    "refreshToken": "rt_abc123..."
}
```

### 3. Testing con Google Authenticator

1. Escanear QR code devuelto en `/api/mfa/setup`
2. Verificar que el código de 6 dígitos coincida
3. Probar backup codes (códigos de 8 caracteres)
4. Verificar remember device functionality

## Merge Strategy

### Opción 1: Integrar a Main
```bash
# Cuando estés satisfecho con la implementación
git checkout main
git merge feature/jwt-refresh-mfa
git push origin main
git branch -d feature/jwt-refresh-mfa
```

### Opción 2: Mantener como Feature
```bash
# Dejar como rama de feature para uso futuro
git checkout main
# La rama feature/jwt-refresh-mfa permanece disponible
```

### Opción 3: Descartar Cambios
```bash
# Si no quieres mantener los cambios
git checkout main
git branch -D feature/jwt-refresh-mfa
git push origin --delete feature/jwt-refresh-mfa
```

## Troubleshooting

### Problemas Comunes

#### 1. TOTP No Valida
- Verificar sincronización de tiempo del servidor
- Asegurar que el secret esté codificado correctamente en Base32
- Probar con ventana de verificación más amplia (Window = 2)

#### 2. Refresh Token Rotation
- Verificar que Data Protection esté configurado correctamente
- Asegurar que las claves persistan entre reinicios
- Validar lógica de rotación en cada uso

#### 3. QR Code Generation
- Verificar que el package QRCoder esté instalado
- Asegurar formato correcto de URI OTP
- Probar con diferentes tamaños de imagen

## Resources Adicionales

- [Google Authenticator](https://support.google.com/accounts/answer/1066447) - Setup guide
- [TOTP RFC 6238](https://tools.ietf.org/html/rfc6238) - Especificación
- [OWASP MFA Guidelines](https://owasp.org/www-community/controls/Multifactor_Authentication) - Mejores prácticas
