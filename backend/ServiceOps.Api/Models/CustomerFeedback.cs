namespace ServiceOps.Api.Models;

public class CustomerFeedback
{
    public int FeedbackId { get; set; }

    public int CustomerId { get; set; }

    public int WorkOrderId { get; set; }

    public int Rating { get; set; }

    public string? Comments { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;

    public WorkOrder WorkOrder { get; set; } = null!;
}