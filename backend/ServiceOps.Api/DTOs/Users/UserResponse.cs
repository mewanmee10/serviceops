namespace ServiceOps.Api.DTOs.Users;

public class UserResponse
{
    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public int RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string AccountStatus { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; }

    public DateTime? PasswordChangedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}