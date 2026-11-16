namespace ServiceOps.Api.Models;

public class Asset
{
    public int AssetId { get; set; }

    public int SiteId { get; set; }

    public string AssetName { get; set; } = string.Empty;

    public string? Category { get; set; }

    public string? SerialNumber { get; set; }

    public DateTime? InstallationDate { get; set; }

    public string? WarrantyInformation { get; set; }

    public CustomerSite CustomerSite { get; set; } = null!;

    public ICollection<ServiceRequest> ServiceRequests { get; set; }
        = new List<ServiceRequest>();

    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; }
        = new List<MaintenanceRecord>();
}