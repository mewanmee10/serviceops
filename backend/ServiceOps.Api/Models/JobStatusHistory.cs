using ServiceOps.Api.Enums;

namespace ServiceOps.Api.Models;

public class JobStatusHistory
{
    public int StatusHistoryId { get; set; }

    public int WorkOrderId { get; set; }

    public ServiceStatus Status { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public string? Remarks { get; set; }

    public WorkOrder WorkOrder { get; set; } = null!;
}