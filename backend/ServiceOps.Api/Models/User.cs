namespace ServiceOps.Api.Models;

public class User
{
    public int UserId { get; set; }

    public int RoleId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; }

    public DateTime? PasswordChangedAt { get; set; }

    public string AccountStatus { get; set; } = "Active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Role Role { get; set; } = null!;

    public Customer? Customer { get; set; }

    public Technician? Technician { get; set; }

    public ICollection<Notification> Notifications { get; set; }
        = new List<Notification>();

    public ICollection<AuditLog> AuditLogs { get; set; }
        = new List<AuditLog>();
}