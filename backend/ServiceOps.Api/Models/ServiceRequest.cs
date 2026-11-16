using ServiceOps.Api.Enums;

namespace ServiceOps.Api.Models;

public class ServiceRequest
{
    public int ServiceRequestId { get; set; }

    public int CustomerId { get; set; }

    public int SiteId { get; set; }

    public int? AssetId { get; set; }

    public string IssueDescription { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public Priority Priority { get; set; }

    public ServiceStatus Status { get; set; } = ServiceStatus.New;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;

    public CustomerSite CustomerSite { get; set; } = null!;

    public Asset? Asset { get; set; }

    public WorkOrder? WorkOrder { get; set; }

    public ICollection<Attachment> Attachments { get; set; }
        = new List<Attachment>();
}