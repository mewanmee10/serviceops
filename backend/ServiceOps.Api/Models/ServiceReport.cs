namespace ServiceOps.Api.Models;

public class ServiceReport
{
    public int ServiceReportId { get; set; }

    public int WorkOrderId { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public string FileLocation { get; set; } = string.Empty;

    public WorkOrder WorkOrder { get; set; } = null!;
}