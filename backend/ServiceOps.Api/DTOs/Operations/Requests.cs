using System.ComponentModel.DataAnnotations;
using ServiceOps.Api.Enums;
namespace ServiceOps.Api.DTOs.Operations;

public record ProfileRequest([Range(1, int.MaxValue)] int UserId, [Required, StringLength(200)] string Name, [StringLength(500)] string? AddressLine = null, [StringLength(100)] string? City = null, [StringLength(100)] string? Province = null, [StringLength(30)] string? PostalCode = null, [Phone, StringLength(30)] string? PhoneNumber = null, bool ReplaceDetails = false, [StringLength(2000)] string? Notes = null);
public record TechnicianRequest([Range(1, int.MaxValue)] int UserId, [Required, StringLength(500)] string Skill, bool Availability);
public record SiteRequest([Range(1, int.MaxValue)] int CustomerId, [Required, StringLength(200)] string SiteName, [Required, StringLength(500)] string AddressLine, [StringLength(100)] string? City, [StringLength(100)] string? Province = null, [StringLength(30)] string? PostalCode = null, [StringLength(1000)] string? LocationDetails = null, bool ReplaceDetails = false, [Range(typeof(decimal), "-90", "90")] decimal? Latitude = null, [Range(typeof(decimal), "-180", "180")] decimal? Longitude = null, [StringLength(2048)] string? MapsUrl = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!ServiceOps.Api.Services.Locations.GoogleMapsLink.IsValid(MapsUrl))
            yield return new ValidationResult("Use a valid HTTPS Google Maps location-sharing link.", [nameof(MapsUrl)]);
        if (Latitude.HasValue != Longitude.HasValue)
            yield return new ValidationResult("Provide both latitude and longitude, or leave both empty.", [nameof(Latitude), nameof(Longitude)]);
    }
}
public record AssetRequest([Range(1, int.MaxValue)] int SiteId, [Required, StringLength(200)] string AssetName, [StringLength(100)] string? Category, [StringLength(100)] string? SerialNumber, DateTimeOffset? InstallationDate = null, [StringLength(2000)] string? WarrantyInformation = null, bool ReplaceDetails = false);
public record ServiceRequestInput([Range(1, int.MaxValue)] int SiteId, [Range(1, int.MaxValue)] int? AssetId, [Required, StringLength(4000)] string IssueDescription, [Required, StringLength(100)] string Category);
public record PriorityRequest([EnumDataType(typeof(Priority))] Priority Priority);
public record AssignmentRequest([Range(1, int.MaxValue)] int TechnicianId, DateTimeOffset? ScheduledVisit, [StringLength(2000)] string? JobNote);
public record JobUpdateRequest([EnumDataType(typeof(ServiceStatus))] ServiceStatus Status, [StringLength(4000)] string? Diagnosis, [StringLength(4000)] string? WorkPerformed, [StringLength(2000)] string? JobNote = null);
public record ChecklistRequest([Required, StringLength(500)] string Description);
public record ChecklistUpdate(bool IsCompleted);
public record FeedbackRequest([Range(1, 5)] int Rating, [StringLength(2000)] string? Comments);
public record PartRequest([Required, StringLength(200)] string PartName, [StringLength(1000)] string? Description, [Range(0, int.MaxValue)] int LowStockLevel);
public record StockRequest([Range(1, int.MaxValue)] int Quantity, [Range(1, int.MaxValue)] int? WorkOrderId, [Required, StringLength(1000)] string Remarks);

public record ServiceRequestEdit([Required, StringLength(4000)] string IssueDescription, [Required, StringLength(100)] string Category);
