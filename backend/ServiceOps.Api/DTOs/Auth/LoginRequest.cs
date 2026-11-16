using System.ComponentModel.DataAnnotations;

namespace ServiceOps.Api.DTOs.Auth;

public class LoginRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(1024)]
    public string Password { get; set; } = string.Empty;
}