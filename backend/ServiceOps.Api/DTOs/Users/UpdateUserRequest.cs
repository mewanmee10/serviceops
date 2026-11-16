using System.ComponentModel.DataAnnotations;

namespace ServiceOps.Api.DTOs.Users;

public class UpdateUserRequest
{
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(32)]
    public string? PhoneNumber { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int RoleId { get; set; }
}