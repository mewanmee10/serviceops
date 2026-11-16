using System.ComponentModel.DataAnnotations;

namespace ServiceOps.Api.Services.Auth;

/// <summary>Shared policy for new credentials; existing hashes remain valid for login.</summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 1024;
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "passwordpassword", "password1234", "password@123", "123456789012", "qwerty12345678"
    };
    public static string? Error(string? password) =>
        string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength || password.Length > MaximumLength
            ? "Choose a password between 12 and 1024 characters."
            : password.Any(char.IsControl) || password.Distinct().Count() < 3 || CommonPasswords.Contains(password)
                ? "Choose a unique password; common or repeated passwords are not permitted."
                : null;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class PasswordPolicyAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        PasswordPolicy.Error(value as string) is string error ? new ValidationResult(error) : ValidationResult.Success;
}
