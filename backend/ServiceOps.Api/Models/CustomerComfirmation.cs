namespace ServiceOps.Api.Models;

public class CustomerConfirmation
{
    public int ConfirmationId { get; set; }

    public int WorkOrderId { get; set; }

    public int CustomerId { get; set; }

    public bool IsConfirmed { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public WorkOrder WorkOrder { get; set; } = null!;

    public Customer Customer { get; set; } = null!;
}