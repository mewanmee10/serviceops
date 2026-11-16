using System.ComponentModel.DataAnnotations;
namespace ServiceOps.Api.DTOs.Auth;

// Role, status, email and customer ownership cannot be mass-assigned here.
public sealed record UpdateProfileRequest(
    [Required, StringLength(100, MinimumLength = 1)] string FullName,
    [StringLength(32), Phone] string? PhoneNumber);
