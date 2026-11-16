namespace ServiceOps.Api.Models;

public class MaintenanceRecord
{
    public int MaintenanceRecordId { get; set; }

    public int AssetId { get; set; }

    public int WorkOrderId { get; set; }

    public string? FaultDescription { get; set; }

    public string? RepairDetails { get; set; }

    public DateTime ServiceDate { get; set; } = DateTime.UtcNow;

    public string? Notes { get; set; }

    public Asset Asset { get; set; } = null!;

    public WorkOrder WorkOrder { get; set; } = null!;
}