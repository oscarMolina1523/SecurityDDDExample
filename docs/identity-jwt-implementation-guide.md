# Guía de Implementación: Identity + JWT Authentication

## Tabla de Contenidos
1. [Introducción](#introducción)
2. [Requerimientos](#requerimientos)
3. [Setup de Rama](#setup-de-rama)
4. [Implementación](#implementación)
5. [Configuración](#configuración)
6. [Migración de Base de Datos](#migración-de-base-de-datos)
7. [Troubleshooting](#troubleshooting)

## Introducción

Esta guía implementa autenticación basada en tokens JWT utilizando ASP.NET Core Identity para gestionar usuarios y roles, proporcionando un sistema stateless ideal para APIs REST y aplicaciones modernas.

### Características Principales
- **Stateless**: No requiere mantenimiento de sesiones en servidor
- **Escalable**: Ideal para microservicios y arquitecturas distribuidas
- **Estándar**: JWT es un estándar RFC 7519 ampliamente adoptado
- **Cross-platform**: Funciona en cualquier plataforma con soporte HTTP
- **Performance**: Validación rápida sin consultas a base de datos

### Casos de Uso Ideales
- APIs REST para aplicaciones móviles
- Microservicios con autenticación distribuida
- Aplicaciones Single Page (SPA)
- Integración con sistemas de terceros

## Requerimientos

### Prerrequisitos
- .NET 8.0 SDK
- SQL Server (como BD principal)
- MongoDB (como BD secundaria, opcional)
- Visual Studio 2022 o VS Code

### Dependencias NuGet
```xml
<PackageReference Include="Microsoft.AspNetCore.Identity" Version="8.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.0" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.0.0" />
```

## Setup de Rama

### Crear Rama de Trabajo
```bash
# Asegurarse de estar en main
git checkout main
git pull origin main

# Crear rama para implementación
git checkout -b feature/identity-jwt

# Push al remoto
git push -u origin feature/identity-jwt
```

### Flujo de Trabajo
```bash
# Durante desarrollo
git add .
git commit -m "feat: implement Identity + JWT authentication"
git push origin feature/identity-jwt

# Para finalizar (opcional)
git checkout main
git merge feature/identity-jwt
git push origin main
git branch -d feature/identity-jwt
```

## Implementación

### 1. Estructura de Archivos

Crear la siguiente estructura siguiendo la arquitectura DDD existente:

```
src/
Domain/
  Entities/
    ApplicationUser.cs
    ApplicationRole.cs
Application/
  DTOs/
    LoginRequest.cs
    LoginResponse.cs
    RegisterRequest.cs
    RegisterResponse.cs
  Interfaces/
    IAuthService.cs
    ITokenService.cs
  Services/
    AuthService.cs
    JwtTokenService.cs
Infrastructure/
  Persistence/
    ApplicationDbContext.cs
  Configuration/
    JwtSettings.cs
    JwtExtensions.cs
DDDExample.API/
  Controllers/
    AuthController.cs
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

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }
}
```

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

### 3. Configuración JWT

#### JwtSettings.cs
```csharp
namespace DDDExample.Infrastructure.Configuration;

public class JwtSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}
```

#### JwtExtensions.cs
```csharp
using DDDExample.Domain.Entities;
using DDDExample.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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

### 4. DTOs

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

#### LoginResponse.cs
```csharp
namespace DDDExample.Application.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
```

#### RegisterRequest.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace DDDExample.Application.DTOs;

public class RegisterRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string Password { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "First name is required")]
    public string FirstName { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Last name is required")]
    public string LastName { get; set; } = string.Empty;
}
```

#### RegisterResponse.cs
```csharp
namespace DDDExample.Application.DTOs;

public class RegisterResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserDto? User { get; set; }
}
```

### 5. Servicios

#### ITokenService.cs
```csharp
using DDDExample.Domain.Entities;

namespace DDDExample.Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(ApplicationUser user);
}
```

#### JwtTokenService.cs
```csharp
using DDDExample.Application.Interfaces;
using DDDExample.Domain.Entities;
using DDDExample.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DDDExample.Application.Services;

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

#### IAuthService.cs
```csharp
using DDDExample.Application.DTOs;

namespace DDDExample.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);
}
```

#### AuthService.cs
```csharp
using DDDExample.Application.DTOs;
using DDDExample.Application.Interfaces;
using DDDExample.Domain.Entities;
using DDDExample.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace DDDExample.Application.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return null;
        }

        user.UpdateLastLogin();
        await _userManager.UpdateAsync(user);

        var token = _tokenService.GenerateToken(user);

        return new LoginResponse
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                CreatedAt = user.CreatedAt
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
}
```

### 6. Controller

#### AuthController.cs
```csharp
using DDDExample.Application.DTOs;
using DDDExample.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        if (result == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
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
}
```

### 7. ApplicationDbContext

```csharp
using DDDExample.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DDDExample.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
        });
    }
}
```

## Configuración

### 1. Program.cs

```csharp
using DDDExample.Application.Interfaces;
using DDDExample.Application.Services;
using DDDExample.Application.Mappings;
using DDDExample.Infrastructure;
using DDDExample.Infrastructure.Configuration;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DDD Example API",
        Version = "v1",
        Description = "A clean architecture example with DDD and JWT authentication"
    });
    
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Add infrastructure services (includes ApplicationDbContext and JWT settings)
builder.Services.AddInfrastructure(builder.Configuration);

// Add JWT authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

// Register application services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

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

### 2. Actualizar DependencyInjection.cs

Actualizar el archivo `src/Infrastructure/DependencyInjection.cs` para incluir los servicios de autenticación:

```csharp
using DDDExample.Application.Interfaces;
using DDDExample.Application.Services;
using DDDExample.Application.Mappings;
using DDDExample.Domain.Repositories;
using DDDExample.Infrastructure.Persistence.MongoDB;
using DDDExample.Infrastructure.Persistence.SqlServer;
using DDDExample.Infrastructure.Repositories.MongoDB;
using DDDExample.Infrastructure.Repositories.SqlServer;
using DDDExample.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

### 3. appsettings.json

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
    "MongoDB": "mongodb://admin:4xFW6B@0jh@localhost:27017/mydb?authSource=admin",
    "SQLServer": "Server=172.30.0.2,1433;Database=mydb;User Id=sa;Password=4xFW6B@0jh;TrustServerCertificate=True;"
  },
  "MongoDBSettings": {
    "ConnectionString": "mongodb://admin:4xFW6B%400jh@localhost:27017/",
    "DatabaseName": "mydb",
    "CollectionName": "entities"
  },
  "SQLServerSettings": {
    "ConnectionString": "Server=localhost,1433;Database=mydb;User Id=sa;Password=4xFW6B@0jh;TrustServerCertificate=True;"
  },
  "Authentication": {
    "Jwt": {
      "Issuer": "DDDExample",
      "Audience": "DDDExampleUsers",
      "SecretKey": "your-super-secret-key-256-bits-long-minimum",
      "ExpirationMinutes": 60
    }
  }
}
```

### 4. appsettings.Development.json

```json
{
  "SQLServerSettings": {
    "ConnectionString": "Server=localhost,1433;Database=mydb_dev;User Id=sa;Password=4xFW6B@0jh;TrustServerCertificate=True;"
  }
}
```

## Migración de Base de Datos

### 1. Crear Migración

```bash
# Instalar herramientas EF si no están instaladas
dotnet tool install --global dotnet-ef

# Crear migración inicial
dotnet ef migrations add InitialIdentityCreate \
    --project src/DDDExample.API/DDDExample.API.csproj \
    --startup-project src/DDDExample.API/DDDExample.API.csproj

# Aplicar migración
dotnet ef database update \
    --project src/DDDExample.API/DDDExample.API.csproj \
    --startup-project src/DDDExample.API/DDDExample.API.csproj
```




## Troubleshooting

### Problemas Comunes

#### 1. Token No Válido
- Verificar que `SecretKey` tenga al menos 256 bits (32 caracteres)
- Asegurar que `Issuer` y `Audience` coincidan en configuración
- Validar que el token no haya expirado

#### 2. Problemas con Base de Datos
- Asegurar que SQL Server LocalDB esté instalado
- Verificar connection string en appsettings.json
- Ejecutar migraciones correctamente

#### 3. CORS Issues
- Agregar configuración CORS si el frontend está en dominio diferente
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

app.UseCors("AllowAll");
```

## Resources Adicionales

- [JWT.io](https://jwt.io/) - Para decodificar y verificar tokens
- [ASP.NET Core Identity Documentation](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/identity)
- [JWT Bearer Authentication](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/jwt)
