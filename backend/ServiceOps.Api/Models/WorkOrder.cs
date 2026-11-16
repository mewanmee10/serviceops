using ServiceOps.Api.Enums;

namespace ServiceOps.Api.Models;

public class WorkOrder
{
    public int WorkOrderId { get; set; }

    public int ServiceRequestId { get; set; }

    public int TechnicianId { get; set; }

    public DateTime? ScheduledVisit { get; set; }

    public string? JobNote { get; set; }

    public string? Diagnosis { get; set; }

    public string? WorkPerformed { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public ServiceStatus Status { get; set; } = ServiceStatus.Assigned;

    public ServiceRequest ServiceRequest { get; set; } = null!;

    public Technician Technician { get; set; } = null!;

    public ICollection<JobStatusHistory> JobStatusHistories { get; set; }
        = new List<JobStatusHistory>();

    public ICollection<ServiceChecklistItem> ChecklistItems { get; set; }
        = new List<ServiceChecklistItem>();

    public ICollection<Attachment> Attachments { get; set; }
        = new List<Attachment>();

    public ICollection<InventoryTransaction> InventoryTransactions { get; set; }
        = new List<InventoryTransaction>();

    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; }
        = new List<MaintenanceRecord>();

    public CustomerConfirmation? CustomerConfirmation { get; set; }

    public ServiceReport? ServiceReport { get; set; }
}