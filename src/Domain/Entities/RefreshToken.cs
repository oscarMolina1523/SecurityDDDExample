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