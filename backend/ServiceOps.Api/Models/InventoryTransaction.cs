using ServiceOps.Api.Enums;

namespace ServiceOps.Api.Models;

public class InventoryTransaction
{
    public int TransactionId { get; set; }

    public int SparePartId { get; set; }

    public int? WorkOrderId { get; set; }

    public InventoryTransactionType TransactionType { get; set; }

    public int Quantity { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    public string? Remarks { get; set; }

    public SparePart SparePart { get; set; } = null!;

    public WorkOrder? WorkOrder { get; set; }
}