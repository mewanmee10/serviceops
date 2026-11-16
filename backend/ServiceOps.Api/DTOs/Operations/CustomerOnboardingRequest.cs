using System.ComponentModel.DataAnnotations;

namespace ServiceOps.Api.DTOs.Operations;

// Role and account status are deliberately absent: this only provisions Customers.
public sealed record CustomerOnboardingRequest(
    [Required, StringLength(100)] string FullName,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, MinLength(8), StringLength(1024)] string Password,
    [Required, StringLength(200)] string CustomerName,
    [Phone, StringLength(32)] string? PhoneNumber);
