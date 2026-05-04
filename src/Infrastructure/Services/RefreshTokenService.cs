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