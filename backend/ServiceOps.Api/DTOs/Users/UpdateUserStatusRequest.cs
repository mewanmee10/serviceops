using System.ComponentModel.DataAnnotations;

namespace ServiceOps.Api.DTOs.Users;

public class UpdateUserStatusRequest
{
    [Required]
    [RegularExpression("^(Active|Inactive)$")]
    public string Status { get; set; } = string.Empty;
}