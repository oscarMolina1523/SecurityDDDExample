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