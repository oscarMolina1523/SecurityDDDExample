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