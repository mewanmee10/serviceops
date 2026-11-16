using System.ComponentModel.DataAnnotations;

namespace ServiceOps.Api.DTOs.Operations;

public sealed record PartsHandoffRequest(
    [Range(1, int.MaxValue)] int SparePartId,
    [Range(1, 1000000)] int Quantity,
    [Required, StringLength(1000)] string Remarks);
