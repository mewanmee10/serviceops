namespace ServiceOps.Api.Models;

public class ServiceChecklistItem
{
    public int ChecklistItemId { get; set; }

    public int WorkOrderId { get; set; }

    public string Description { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    public WorkOrder WorkOrder { get; set; } = null!;
}