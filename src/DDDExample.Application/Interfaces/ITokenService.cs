using DDDExample.Domain.Entities;

namespace DDDExample.Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(ApplicationUser user);
}