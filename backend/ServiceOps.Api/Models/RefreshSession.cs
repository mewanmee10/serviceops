namespace ServiceOps.Api.Models;

public sealed class RefreshSession
{
    public Guid RefreshSessionId { get; set; }
    public int UserId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string PasswordVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? RotatedAt { get; set; }
    public Guid? ReplacedById { get; set; }
    public User User { get; set; } = null!;
}
