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