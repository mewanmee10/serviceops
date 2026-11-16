using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Data;
using ServiceOps.Api.DTOs.Operations;
using ServiceOps.Api.Enums;
using ServiceOps.Api.Models;
using ServiceOps.Api.Services.Interfaces;
namespace ServiceOps.Api.Controllers;

[Authorize(Policy = "permission:inventory.manage")]
[ApiController, Route("api/inventory"), Authorize(Roles = "Administrator,Inventory Staff"), TypeFilter(typeof(AccountApiExceptionFilter))]
public class InventoryController(AppDbContext db) : ControllerBase
{
    [HttpGet("audit")]
    public async Task<IActionResult> Audit(int page = 1, string? search = null, DateTimeOffset? from = null, DateTimeOffset? to = null, string sort = "desc")
    {
        if (page < 1 || page > 1000000 || from > to || search?.Length > 200 || (sort != "asc" && sort != "desc")) return BadRequest();
        var query = db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "SparePart" || a.EntityType == "InventoryTransaction");
        query = query.Where(a => (from == null || a.CreatedAt >= from.Value.UtcDateTime) && (to == null || a.CreatedAt <= to.Value.UtcDateTime));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(a => a.User.FullName.Contains(search) || a.Action.Contains(search) || a.EntityType.Contains(search));
        var ordered = sort == "asc" ? query.OrderBy(a => a.CreatedAt).ThenBy(a => a.AuditLogId) : query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AuditLogId);
        return Ok(new { Total = await query.CountAsync(), Page = page, PageSize = 25,
            Items = await ordered.Skip((page - 1) * 25).Take(25)
                .Select(a => new { a.AuditLogId, Actor = a.User.FullName, ActorRole = a.User.Role.RoleName, a.Action, a.EntityType, a.EntityId, a.CreatedAt }).ToListAsync() });
    }
    [HttpGet("parts")]
    public async Task<IActionResult> Parts(bool lowStock = false) => Ok(await db.SpareParts.AsNoTracking().Where(p => !lowStock || p.QuantityInStock <= p.LowStockLevel).OrderBy(p => p.PartName).Select(p => new { p.SparePartId, p.PartName, p.Description, p.QuantityInStock, p.LowStockLevel }).ToListAsync());
    [HttpGet("parts/{id:int}")]
    public async Task<IActionResult> Part(int id)
    {
        var part = await db.SpareParts.AsNoTracking().Where(p => p.SparePartId == id)
            .Select(p => new { p.SparePartId, p.PartName, p.Description, p.QuantityInStock, p.LowStockLevel }).SingleOrDefaultAsync();
        return part == null ? NotFound() : Ok(part);
    }

    [HttpPost("parts")]
    public async Task<IActionResult> Create(PartRequest input)
    { var p = new SparePart { PartName = input.PartName.Trim(), Description = input.Description, LowStockLevel = input.LowStockLevel }; db.SpareParts.Add(p); await db.SaveChangesAsync(); return Created($"/api/inventory/parts/{p.SparePartId}", new { p.SparePartId, p.PartName, p.QuantityInStock, p.LowStockLevel }); }
    [HttpPut("parts/{id:int}")]
    public async Task<IActionResult> Update(int id, PartRequest input)
    { var p = await db.SpareParts.SingleOrDefaultAsync(p => p.SparePartId == id); if (p == null) return NotFound(); p.PartName = input.PartName.Trim(); p.Description = input.Description; p.LowStockLevel = input.LowStockLevel; await db.SaveChangesAsync(); return NoContent(); }
    [HttpDelete("parts/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    { using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"SpareParts\", \"InventoryTransactions\" IN SHARE ROW EXCLUSIVE MODE"); var p = await db.SpareParts.SingleOrDefaultAsync(p => p.SparePartId == id); if (p == null) return NotFound(); if (p.QuantityInStock != 0 || await db.InventoryTransactions.AnyAsync(t => t.SparePartId == id)) return Conflict(new { message = "Only unused zero-stock parts may be deleted." }); db.Remove(p); await db.SaveChangesAsync(); await tx.CommitAsync(); return NoContent(); }
    [HttpGet("work-orders")]
    public async Task<IActionResult> EligibleWorkOrders() => Ok(await db.WorkOrders.AsNoTracking()
        .Where(w => w.Status == ServiceStatus.InProgress)
        .Select(w => new { w.WorkOrderId, w.ServiceRequestId, w.ServiceRequest.IssueDescription }).ToListAsync());

    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions(int? partId, int? workOrderId, InventoryTransactionType? type, DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from > to || (type != null && !Enum.IsDefined(type.Value))) return BadRequest();
        return Ok(await db.InventoryTransactions.AsNoTracking().Where(t => (partId == null || t.SparePartId == partId) && (workOrderId == null || t.WorkOrderId == workOrderId) && (type == null || t.TransactionType == type) && (from == null || t.TransactionDate >= from.Value.UtcDateTime) && (to == null || t.TransactionDate <= to.Value.UtcDateTime)).OrderByDescending(t => t.TransactionId).Select(t => new { t.TransactionId, t.SparePartId, PartName = t.SparePart.PartName, t.WorkOrderId, t.TransactionType, t.Quantity, t.TransactionDate, t.Remarks }).ToListAsync());
    }
    [HttpPost("parts/{id:int}/add")]
    public Task<IActionResult> Add(int id, StockRequest input) => Change(id, input, true);
    [HttpPost("parts/{id:int}/issue")]
    public Task<IActionResult> Issue(int id, StockRequest input) => Change(id, input, false);
    public record AdjustmentRequest([System.ComponentModel.DataAnnotations.Range(-1000000, 1000000)] int Delta, [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(1000)] string Remarks);
    [HttpPost("parts/{id:int}/adjust")]
    public async Task<IActionResult> Adjust(int id, AdjustmentRequest input)
    {
        if (input.Delta == 0) return BadRequest(new { message = "Adjustment must be nonzero." });
        using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"SpareParts\", \"InventoryTransactions\" IN SHARE ROW EXCLUSIVE MODE");
        var part = await db.SpareParts.SingleOrDefaultAsync(p => p.SparePartId == id); if (part == null) return NotFound();
        var amount = (long)part.QuantityInStock + input.Delta; if (amount < 0 || amount > int.MaxValue) return Conflict(new { message = "Adjustment would produce invalid stock." });
        part.QuantityInStock = (int)amount; var movement = new InventoryTransaction { SparePartId = id, Quantity = input.Delta, TransactionType = InventoryTransactionType.Adjustment, Remarks = input.Remarks.Trim() }; db.InventoryTransactions.Add(movement);
        await LowStock(part); await db.SaveChangesAsync(); await tx.CommitAsync(); return Ok(new { part.SparePartId, part.QuantityInStock, movement.TransactionId });
    }
    [HttpPost("parts/{id:int}/return")]
    public Task<IActionResult> Return(int id, StockRequest input) => Reconcile(id, input, true);
    [HttpPost("parts/{id:int}/consume")]
    public Task<IActionResult> Consume(int id, StockRequest input) => Reconcile(id, input, false);
    private async Task<IActionResult> Reconcile(int id, StockRequest input, bool returned)
    {
        using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"WorkOrders\", \"SpareParts\", \"InventoryTransactions\" IN SHARE ROW EXCLUSIVE MODE");
        var part = await db.SpareParts.SingleOrDefaultAsync(p => p.SparePartId == id); if (part == null) return NotFound();
        if (input.WorkOrderId == null || !await db.WorkOrders.AnyAsync(w => w.WorkOrderId == input.WorkOrderId && w.Status == ServiceStatus.InProgress)) return BadRequest(new { message = "Reconciliation requires an in-progress work order." });
        var transactions = await db.InventoryTransactions.Where(t => t.SparePartId == id && t.WorkOrderId == input.WorkOrderId).ToListAsync();
        var outstanding = transactions.Where(t => t.TransactionType == InventoryTransactionType.PartIssue).Sum(t => (long)t.Quantity) - transactions.Where(t => t.TransactionType is InventoryTransactionType.PartConsumption or InventoryTransactionType.StockAddition).Sum(t => (long)t.Quantity);
        if (input.Quantity > outstanding) return Conflict(new { message = "Quantity exceeds issued, unconsumed and unreturned parts." });
        if (returned && (long)part.QuantityInStock + input.Quantity > int.MaxValue) return Conflict(new { message = "Stock quantity is too large." });
        if (returned) part.QuantityInStock += input.Quantity;
        var movement = new InventoryTransaction { SparePartId = id, WorkOrderId = input.WorkOrderId, Quantity = input.Quantity, TransactionType = returned ? InventoryTransactionType.StockAddition : InventoryTransactionType.PartConsumption, Remarks = input.Remarks.Trim() }; db.InventoryTransactions.Add(movement);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Ok(new { part.SparePartId, part.QuantityInStock, movement.TransactionId });
    }
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from > to) return BadRequest();
        var movements = db.InventoryTransactions.Where(t => (from == null || t.TransactionDate >= from.Value.UtcDateTime) && (to == null || t.TransactionDate <= to.Value.UtcDateTime));
        return Ok(new { Parts = await db.SpareParts.CountAsync(), UnitsInStock = await db.SpareParts.SumAsync(p => (long)p.QuantityInStock), LowStock = await db.SpareParts.AsNoTracking().Where(p => p.QuantityInStock <= p.LowStockLevel).Select(p => new { p.SparePartId, p.PartName, p.QuantityInStock, p.LowStockLevel }).ToListAsync(), Movements = await movements.GroupBy(t => t.TransactionType).Select(g => new { Type = g.Key, Transactions = g.Count(), Quantity = g.Sum(t => (long)t.Quantity) }).ToListAsync() });
    }
    private async Task LowStock(SparePart part)
    {
        if (part.QuantityInStock > part.LowStockLevel) return;
        var ids = await db.Users.Where(u => u.Role.RoleName == "Inventory Staff" && u.AccountStatus == "Active").Select(u => u.UserId).ToListAsync();
        foreach (var id in ids) db.Notifications.Add(new Notification { UserId = id, Message = $"Low stock: {part.PartName} ({part.QuantityInStock} remaining)." });
    }
    private async Task<IActionResult> Change(int id, StockRequest input, bool addition)
    {
        using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"WorkOrders\", \"SpareParts\", \"InventoryTransactions\" IN SHARE ROW EXCLUSIVE MODE");
        var p = await db.SpareParts.SingleOrDefaultAsync(p => p.SparePartId == id); if (p == null) return NotFound();
        if (addition && input.WorkOrderId != null) return BadRequest(new { message = "Stock additions must not reference a work order." });
        if (!addition && (input.WorkOrderId == null || !await db.WorkOrders.AnyAsync(w => w.WorkOrderId == input.WorkOrderId && w.Status == ServiceStatus.InProgress))) return BadRequest(new { message = "Parts must be issued to an in-progress work order." });
        if (!addition && p.QuantityInStock < input.Quantity) return Conflict(new { message = "Insufficient stock." });
        if (addition && p.QuantityInStock > int.MaxValue - input.Quantity) return BadRequest(new { message = "Stock quantity is too large." });
        p.QuantityInStock += addition ? input.Quantity : -input.Quantity; var t = new InventoryTransaction { SparePartId = id, WorkOrderId = input.WorkOrderId, Quantity = input.Quantity, TransactionType = addition ? InventoryTransactionType.StockAddition : InventoryTransactionType.PartIssue, Remarks = input.Remarks.Trim() }; db.InventoryTransactions.Add(t);
        if (p.QuantityInStock <= p.LowStockLevel) { var ids = await db.Users.Where(u => u.Role.RoleName == "Inventory Staff" && u.AccountStatus == "Active").Select(u => u.UserId).ToListAsync(); foreach (var uid in ids) db.Notifications.Add(new Notification { UserId = uid, Message = $"Low stock: {p.PartName} ({p.QuantityInStock} remaining)." }); }
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Ok(new { p.SparePartId, p.QuantityInStock, t.TransactionId });
    }
}
