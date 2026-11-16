namespace ServiceOps.Api.Models;

public class SparePart
{
    public int SparePartId { get; set; }

    public string PartName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int QuantityInStock { get; set; }

    public int LowStockLevel { get; set; }

    public ICollection<InventoryTransaction> InventoryTransactions { get; set; }
        = new List<InventoryTransaction>();
}