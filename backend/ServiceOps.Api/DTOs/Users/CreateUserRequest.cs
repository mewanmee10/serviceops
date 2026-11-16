using System.ComponentModel.DataAnnotations;
using ServiceOps.Api.Services.Auth;

namespace ServiceOps.Api.DTOs.Users;

public class CreateUserRequest
{
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [PasswordPolicy]
    [StringLength(1024)]
    public string Password { get; set; } = string.Empty;

    [Required, StringLength(1024), Compare(nameof(Password), ErrorMessage = "Passwords must match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Phone]
    [StringLength(32)]
    public string? PhoneNumber { get; set; }

    [StringLength(200)]
    public string? CustomerName { get; set; }

    [StringLength(500)]
    public string? TechnicianSkill { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int RoleId { get; set; }
}