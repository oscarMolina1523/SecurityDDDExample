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

DDDExample.API

```xml
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.0" />
	<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
	<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.0">
		<PrivateAssets>all</PrivateAssets>
		<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
	</PackageReference>
```

DDDExample.Application

```xml
		<FrameworkReference Include="Microsoft.AspNetCore.App" />
		<PackageReference Include="AutoMapper" Version="13.0.1" />
		<PackageReference Include="Microsoft.IdentityModel.Tokens" Version="8.0.0" />
		<PackageReference Include="Otp.NET" Version="1.4.1" />
		<PackageReference Include="QRCoder" Version="1.8.0" />
		<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.0.0" />
```

Infrastructure

```xml
		<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
		<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
		<PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
		<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
		<PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="8.0.0" />
		<PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="8.0.0" />
		<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
		<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.0" />
		<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.0" />
		<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
		<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="8.0.0" />
		<PackageReference Include="Microsoft.Extensions.Options" Version="8.0.0" />
		<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
		<PackageReference Include="Otp.NET" Version="1.4.1" />
		<PackageReference Include="QRCoder" Version="1.8.0" />
		<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.0.0" />
		<PackageReference Include="MongoDB.Driver" Version="2.22.0" />
		<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0">
			<PrivateAssets>all</PrivateAssets>
			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
		</PackageReference>
```

### Referencias de capas

DDDExample.API

```xml
   <ItemGroup>
		<ProjectReference Include="..\DDDExample.Application\DDDExample.Application.csproj" />
		<ProjectReference Include="..\Infrastructure\DDDExample.Infrastructure.csproj" />
	</ItemGroup>
```

DDDExample.Application

```xml
   <ItemGroup>
		<ProjectReference Include="..\Domain\DDDExample.Domain.csproj" />
	</ItemGroup>
```

Infrastructure

```xml
   <ItemGroup>
	    <ProjectReference Include="..\DDDExample.Application\DDDExample.Application.csproj" />
		<ProjectReference Include="..\Domain\DDDExample.Domain.csproj" />
	</ItemGroup>
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
Domain/
  Entities/
    ApplicationUser.cs
    ApplicationRole.cs
    RefreshToken.cs
    UserSession.cs
Application/
  Common/
    JwtSettings.cs
  DTOs/
    LoginRequest.cs
    LoginResponse.cs
    RegisterRequest.cs
    RegisterResponse.cs
    MfaSetupRequest.cs
    MfaSetupResponse.cs
    MfaVerifyRequest.cs
    RefreshTokenRequest.cs
    DisableMfaRequest.cs
    EnableMfaRequest.cs
  Interfaces/
    IAuthService.cs
    ITokenService.cs
    IMfaService.cs
    IRefreshTokenService.cs
Infrastructure/
  Persistence/
    sqlServer/
        ApplicationDbContext.cs
  Configuration/
    JwtExtensions.cs
    JwtMfaExtensions.cs
    MfaSettings.cs
    RefreshTokenSettings.cs
  Services/
    AuthService.cs
    JwtTokenService.cs
    RefreshTokenService.cs
    TotpMfaService.cs
DDDExample.API/
  Controllers/
    AuthController.cs
    MfaController.cs
  Middleware/
    MfaRequiredAttribute.cs
```

### 2. Entidades de Dominio

#### ApplicationUser.cs
```csharp
using Microsoft.AspNetCore.Identity;
namespace DDDExample.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    // Propiedades MFA
    public bool MfaEnabled { get; set; }
    public string? MfaSecret { get; set; }
    public DateTime? MfaSetupCompleted { get; set; }
    public string? BackupCodes { get; set; } // JSON array

    public string FullName => $"{FirstName} {LastName}";

    // For EF Core
    private ApplicationUser() { }

    public ApplicationUser(string email, string firstName, string lastName)
    {
        Email = email;
        UserName = email;
        FirstName = firstName;
        LastName = lastName;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateLastLogin() { LastLoginAt = DateTime.UtcNow; }

    // Navigation properties
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public virtual ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();
}
```

#### RefreshToken.cs
```csharp
using DDDExample.Domain.Entities;

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
namespace DDDExample.Domain.Entities;

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

### 3. Application

#### Carpeta Common
#### JwtSettings.cs
```csharp
namespace DDDExample.Application.Common;

public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 15; // Más corto para mayor seguridad
}
```

#### Carpeta DTOs
#### MfaSetupRequest.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace DDDExample.Application.DTOs;

public class MfaSetupRequest
{
    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}
```

#### MfaSetupResponse.cs
```csharp
namespace DDDExample.Application.DTOs;

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

namespace DDDExample.Application.DTOs;

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

namespace DDDExample.Application.DTOs;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "Refresh token is required")]
    public string RefreshToken { get; set; } = string.Empty;
}
```

#### DisableMfaRequest.cs
```csharp
namespace DDDExample.Application.DTOs;

public class DisableMfaRequest
{
    public string Password { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
}
```

#### EnableMfaRequest.cs
```csharp
namespace DDDExample.Application.DTOs;

public class EnableMfaRequest
{
    public string VerificationCode { get; set; } = string.Empty;
}
```

#### LoginResponse.cs
```csharp
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
```

#### LoginRequest.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace DDDExample.Application.DTOs;

public class LoginRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;
}
```

#### Carpeta Interfaces
#### IAuthService.cs
```csharp
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
```

#### IMfaService.cs
```csharp
namespace DDDExample.Application.Interfaces;

public interface IMfaService
{
    string GenerateSecret();
    string GenerateQrCode(string secret, string userEmail, string issuer);
    bool VerifyToken(string secret, string token);
    List<string> GenerateBackupCodes();
}
```

#### IRefreshTokenService.cs
```csharp
namespace DDDExample.Application.Interfaces;

public interface IRefreshTokenService
{
    Task<string> GenerateRefreshTokenAsync(Guid userId);
    Task<bool> ValidateRefreshTokenAsync(string token);
    Task<string> RotateRefreshTokenAsync(string currentToken);
    Task RevokeUserTokensAsync(Guid userId);
    Task RevokeAllUserTokensAsync(Guid userId);
}
```

#### ITokenService.cs
```csharp
using DDDExample.Domain.Entities;

namespace DDDExample.Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(ApplicationUser user);
}
```

### 4. Domain

#### Carpeta Entities
#### ApplicationRole.cs
```csharp
using Microsoft.AspNetCore.Identity;

namespace DDDExample.Domain.Entities;

public class ApplicationRole : IdentityRole<Guid>
{
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    // For EF Core
    private ApplicationRole() { }

    public ApplicationRole(string name, string description = "") : base(name)
    {
        Description = description;
        CreatedAt = DateTime.UtcNow;
    }
}
```

#### ApplicationUser.cs
```csharp
using Microsoft.AspNetCore.Identity;
namespace DDDExample.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    // Propiedades MFA
    public bool MfaEnabled { get; set; }
    public string? MfaSecret { get; set; }
    public DateTime? MfaSetupCompleted { get; set; }
    public string? BackupCodes { get; set; } // JSON array

    public string FullName => $"{FirstName} {LastName}";

    // For EF Core
    private ApplicationUser() { }

    public ApplicationUser(string email, string firstName, string lastName)
    {
        Email = email;
        UserName = email;
        FirstName = firstName;
        LastName = lastName;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateLastLogin() { LastLoginAt = DateTime.UtcNow; }

    // Navigation properties
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public virtual ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();
}
```

#### RefreshToken.cs
```csharp
using DDDExample.Domain.Entities;

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
namespace DDDExample.Domain.Entities;

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
### 5. Infrastructure

#### Carpeta Configuration
#### RefreshTokenSettings.cs
```csharp
namespace DDDExample.Infrastructure.Configuration
{
    public class RefreshTokenSettings
    {
        public int RefreshTokenExpirationDays { get; set; } = 30;
        public bool RotationEnabled { get; set; } = true;
        public int MaxActiveTokens { get; set; } = 5;
        public int CleanupIntervalHours { get; set; } = 24;
    }
}
```

#### MfaSettings.cs
```csharp
namespace DDDExample.Infrastructure.Configuration { 
    public class MfaSettings
    {
        public string Issuer { get; set; } = "DDDExample";
        public int Digits { get; set; } = 6;
        public int Period { get; set; } = 30;
        public int Window { get; set; } = 1;
        public int BackupCodesCount { get; set; } = 10;
        public int RememberDeviceDays { get; set; } = 30;
    }
}
```

#### JwtExtension.cs
```csharp
using DDDExample.Domain.Entities;
using DDDExample.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DDDExample.Application.Common;
using DDDExample.Infrastructure.Persistence.SqlServer;

namespace DDDExample.Infrastructure.Configuration;

public static class JwtExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Authentication:Jwt").Get<JwtSettings>();

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }
}
```

#### JwtMfaExtensions.cs
```csharp
// Infrastructure/Configuration/JwtMfaExtensions.cs

using System.Text;
using DDDExample.Domain.Entities;
using DDDExample.Infrastructure.Persistence.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace DDDExample.Infrastructure.Configuration;

public static class JwtMfaExtensions
{
    public static IServiceCollection AddJwtMfaAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Identity
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;

            // MFA
            options.SignIn.RequireConfirmedEmail = false;
            options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // JWT
        var jwtKey = configuration["Authentication:Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey not configured");

        var issuer = configuration["Authentication:Jwt:Issuer"]
            ?? throw new InvalidOperationException("JWT Issuer not configured");

        var audience = configuration["Authentication:Jwt:Audience"]
            ?? throw new InvalidOperationException("JWT Audience not configured");

        var key = Encoding.UTF8.GetBytes(jwtKey);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),

                ValidateIssuer = true,
                ValidIssuer = issuer,

                ValidateAudience = true,
                ValidAudience = audience,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        return services;
    }
}
```

#### Carpeta Services
#### AuthService.cs
```csharp
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
```

#### JwtTokenService.cs 
```csharp
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using DDDExample.Infrastructure.Persistence.SqlServer;
using DDDExample.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DDDExample.Application.Common; 
using DDDExample.Domain.Entities;
using DDDExample.Application.Interfaces;

namespace DDDExample.Infrastructure.Services;

public class JwtTokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;

    public JwtTokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public string GenerateToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

#### RefreshTokenService.cs
```csharp
using DDDExample.Infrastructure.Configuration;
using DDDExample.Infrastructure.Persistence.SqlServer;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using DDDExample.Application.Interfaces;
using System.Security;
using System.Linq;

namespace DDDExample.Infrastructure.Services;

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

    public async Task RevokeAllUserTokensAsync(Guid userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(x => x.UserId == userId && !x.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.Revoke();
        }

        await _context.SaveChangesAsync();
    }
}
```

#### TotpMfaService.cs
```csharp
using DDDExample.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using OtpNet;
using QRCoder;
using DDDExample.Infrastructure.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using DDDExample.Domain.Entities;
using DDDExample.Application.Interfaces;

namespace DDDExample.Infrastructure.Services;

public class TotpMfaService : IMfaService
{
    private readonly MfaSettings _settings;

    public TotpMfaService(IOptions<MfaSettings> settings)
    {
        _settings = settings.Value;
    }

    public string GenerateSecret()
    {
        return Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
    }

    public string GenerateQrCode(string secret, string userEmail, string issuer)
    {
        var provisionUri = new OtpUri(
            OtpType.Totp,
            secret,
            userEmail,
            issuer,
            OtpHashMode.Sha1,
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
            codes.Add(Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(8)).Substring(0, 8));
        }
        return codes;
    }
}
```

#### DependencyInjection.cs
```csharp
using DDDExample.Infrastructure.Services;
using DDDExample.Application.Interfaces;
using DDDExample.Domain.Repositories;
using DDDExample.Infrastructure.Persistence.MongoDB;
using DDDExample.Infrastructure.Persistence.SqlServer;
using DDDExample.Infrastructure.Repositories.MongoDB;
using DDDExample.Infrastructure.Repositories.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using DDDExample.Application.Common;
using Microsoft.AspNetCore.Identity;
using DDDExample.Domain.Entities;

namespace DDDExample.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure SQL Server settings
        var sqlServerSettings = configuration.GetSection("SQLServerSettings").Get<SqlServerSettings>() 
            ?? throw new InvalidOperationException("SQLServerSettings configuration section is missing or invalid");
        
        // Configure SQL Server for Products
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                sqlServerSettings.ConnectionString,
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));


        // Register SQL Server Product Repository
        services.AddScoped<IRepository<Domain.Entities.Product, Guid>, SqlProductRepository>();

        // Configure MongoDB settings
        var mongoDbSettings = configuration.GetSection("MongoDBSettings").Get<MongoDbSettings>()
            ?? throw new InvalidOperationException("MongoDBSettings configuration section is missing or invalid");

        // Register MongoDB context with settings
        services.AddSingleton<MongoDbContext>(_ =>
            new MongoDbContext(Options.Create(mongoDbSettings)));

        // Register MongoDB Category Repository
        services.AddScoped<IRepository<Domain.Entities.Category, string>>(sp =>
            new MongoCategoryRepository(sp.GetRequiredService<MongoDbContext>()));

        // Configure JWT settings
        var jwtSettings = configuration.GetSection("Authentication:Jwt").Get<JwtSettings>();
        if (jwtSettings != null)
        {
            services.Configure<JwtSettings>(configuration.GetSection("Authentication:Jwt"));
        }

        return services;
    }
}
```

## DDDExample.API

### Carpeta Controllers
### AuthController.cs

```csharp
using DDDExample.Application.DTOs;
using DDDExample.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DDDExample.API.Controllers;

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
        var result = await _authService.RefreshTokenAsync(request);

        if (string.IsNullOrEmpty(result.Token))
        {
            return BadRequest(new { message = "Invalid refresh token" });
        }

        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(nameof(Login), new { }, result);
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetProfile()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Aquí podrías obtener el usuario desde la base de datos
        return Ok(new UserDto
        {
            Id = Guid.Parse(userId),
            Email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty,
            FullName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        });
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

    [HttpPost("setup-mfa")]
    [Authorize]
    public async Task<ActionResult<MfaSetupResponse>> SetupMfa([FromBody] MfaSetupRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _authService.SetupMfaAsync(Guid.Parse(userId), request);

        return Ok(result);
    }

    //[HttpPost("enable-mfa")]
    //[Authorize]
    //public async Task<IActionResult> EnableMfa([FromBody] MfaVerifyRequest request)
    //{
    //    var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    //    if (string.IsNullOrEmpty(userId))
    //        return Unauthorized();

    //    var success = await _authService.EnableMfaAsync(Guid.Parse(userId), request.Code);

    //    if (!success)
    //        return BadRequest(new { message = "Invalid MFA code" });

    //    return Ok(new { message = "MFA enabled successfully" });
    //}

    [HttpPost("enable-mfa")]
    [Authorize]
    public async Task<IActionResult> EnableMfa([FromBody] EnableMfaRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var code = request.VerificationCode.Replace(" ", "");

        var success = await _authService.EnableMfaAsync(
            Guid.Parse(userId),
            code
        );

        if (!success)
            return BadRequest(new { message = "Invalid MFA code" });

        return Ok(new
        {
            message = "MFA enabled successfully"
        });
    }

    [HttpPost("disable-mfa")]
    [Authorize]
    public async Task<IActionResult> DisableMfa()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var success = await _authService.DisableMfaAsync(Guid.Parse(userId));

        if (!success)
            return BadRequest(new { message = "Failed to disable MFA" });

        return Ok(new { message = "MFA disabled successfully" });
    }
}
```

### MfaController.cs

```csharp
using DDDExample.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DDDExample.Application.Interfaces;
using DDDExample.Domain.Entities;
using System.Text.Json;

namespace DDDExample.API.Controllers;

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

```
### Carpeta Middleware
### MfaRequiredAttribute.cs

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using DDDExample.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DDDExample.API.Middleware;

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

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "MongoDB": "mongodb://admin:admin123@localhost:27017/mydb?authSource=admin",
    "SQLServer": "Server=localhost,1433;Database=mydb;User Id=sa;Password=SqlPassword2026!;TrustServerCertificate=True;"
  },
  "MongoDBSettings": {
    "ConnectionString": "mongodb://admin:admin123@localhost:27017",
    "DatabaseName": "mydb",
    "CollectionName": "entities"
  },
  "SQLServerSettings": {
    "ConnectionString": "Server=localhost,1433;Database=mydb;User Id=sa;Password=SqlPassword2026!;TrustServerCertificate=True;"
  },
  "Authentication": {
    "Jwt": {
      "Issuer": "DDDExample",
      "Audience": "DDDExampleUsers",
      "SecretKey": "your-super-secret-key-256-bits-long-minimum",
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
  }  
}

//"SQLServerSettings": {
//    "ConnectionString": "Server=localhost,1433;Database=mydb;User Id=sa;Password=g7B4Rj75wU;TrustServerCertificate=True;"
//  }

  //para conexion a sql server local en managemnet studio con authenticaion de windows
//"SQLServerSettings": {
//    "ConnectionString": "Server=.,1433;Database=mydb;Trusted_Connection=True;TrustServerCertificate=True;"
//  }
```

### Program.cs

```csharp
using DDDExample.Application.Interfaces;
using DDDExample.Application.Services;
using DDDExample.Application.Mappings;
using DDDExample.Infrastructure;
using DDDExample.Infrastructure.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using DDDExample.Infrastructure.Services;
using DDDExample.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Data Protection para refresh tokens
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("keys"))
    .SetApplicationName("DDDExample");


// Add AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Add infrastructure services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtMfaAuthentication(builder.Configuration);

// Register application services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

// Register services
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMfaService, TotpMfaService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

var app = builder.Build();

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        // Apply pending migrations
        if (context.Database.GetPendingMigrations().Any())
        {
            Console.WriteLine("Aplicando migraciones pendientes...");
            context.Database.Migrate();
            Console.WriteLine("Migraciones aplicadas exitosamente.");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al aplicar las migraciones.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DDD Example API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
```


## Testing

### 1. Crear Migración

```bash
# Crear migración para MFA y refresh tokens
dotnet ef migrations add InitialIdentityWithMfa --project src/Infrastructure/DDDExample.Infrastructure.csproj --startup-project src/DDDExample.API/DDDExample.API.csproj --context ApplicationDbContext

# Aplicar migración
dotnet ef database update --project src/Infrastructure/DDDExample.Infrastructure.csproj --startup-project src/DDDExample.API/DDDExample.API.csproj --context ApplicationDbContext
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
