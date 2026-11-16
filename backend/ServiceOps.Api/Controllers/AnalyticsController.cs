using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Data;
using ServiceOps.Api.Enums;
using ServiceOps.Api.Services.Interfaces;
using ServiceOps.Api.Services.Operations;
namespace ServiceOps.Api.Controllers;

[ApiController, Route("api/analytics"), Authorize(Policy = "permission:analytics.read"), TypeFilter(typeof(AccountApiExceptionFilter))]
public sealed class AnalyticsController(AppDbContext db, OperationsAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(DateTimeOffset? from, DateTimeOffset? to)
    {
        var start = (from ?? DateTimeOffset.UtcNow.AddMonths(-12)).UtcDateTime;
        var end = (to ?? DateTimeOffset.UtcNow).UtcDateTime;
        if (start > end || end - start > TimeSpan.FromDays(3660)) return BadRequest(new { message = "Select an ordered date interval of at most ten years." });
        var requests = await access.Requests.AsNoTracking().Where(r => r.CreatedAt >= start && r.CreatedAt <= end).Select(r => new { r.ServiceRequestId, r.Status, r.Priority, r.AssetId, AssetName = r.Asset == null ? null : r.Asset.AssetName }).ToListAsync();
        // Completion metrics use completion date; workload uses request creation date.
        var jobs = await access.Jobs.AsNoTracking().Where(w => (w.EndTime >= start && w.EndTime <= end) || (w.ServiceRequest.CreatedAt >= start && w.ServiceRequest.CreatedAt <= end)).Select(w => new { w.WorkOrderId, w.TechnicianId, Technician = w.Technician.User.FullName, w.Status, w.ScheduledVisit, w.StartTime, w.EndTime, RequestedAt = w.ServiceRequest.CreatedAt }).ToListAsync();
        var completed = jobs.Where(w => w.EndTime >= start && w.EndTime <= end && (w.Status == ServiceStatus.Completed || w.Status == ServiceStatus.Closed)).ToList();
        var feedback = await db.CustomerFeedbacks.AsNoTracking().Where(f => access.Jobs.Any(w => w.WorkOrderId == f.WorkOrderId) && f.SubmittedAt >= start && f.SubmittedAt <= end).Select(f => new { f.Rating, f.WorkOrder.TechnicianId }).ToListAsync();
        var inventory = User.IsInRole("Administrator") || User.IsInRole("Inventory Staff");
        var durations = completed.Where(w => w.StartTime != null && w.EndTime >= w.StartTime).Select(w => (w.EndTime!.Value - w.StartTime!.Value).TotalHours).ToList();
        var responses = jobs.Where(w => w.RequestedAt >= start && w.RequestedAt <= end && w.StartTime >= w.RequestedAt)
            .Select(w => (w.StartTime!.Value - w.RequestedAt).TotalHours).ToList();
        var movement = inventory || User.IsInRole("Service Manager") ? await db.InventoryTransactions.AsNoTracking()
            .Where(t => t.TransactionDate >= start && t.TransactionDate <= end &&
                (inventory || (t.WorkOrderId != null && access.Jobs.Any(w => w.WorkOrderId == t.WorkOrderId) && t.TransactionType == InventoryTransactionType.PartConsumption)))
            .Select(t => new { t.TransactionDate, t.TransactionType, t.Quantity }).ToListAsync() : null;
        return Ok(new
        {
            From = start,
            To = end,
            OpenRequests = requests.Count(r => r.Status != ServiceStatus.Completed && r.Status != ServiceStatus.Closed),
            CompletedJobs = completed.Count,
            OverdueJobs = jobs.Count(w => w.ScheduledVisit < DateTime.UtcNow && w.Status != ServiceStatus.Completed && w.Status != ServiceStatus.Closed),
            AverageCompletionHours = durations.Count > 0 ? (double?)Math.Round(durations.Average(), 2) : null,
            AverageResponseHours = responses.Count > 0 ? (double?)Math.Round(responses.Average(), 2) : null,
            FrequentlyFailingAssets = requests.Where(r => r.AssetId != null).GroupBy(r => new { r.AssetId, r.AssetName })
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.AssetId).Take(10)
                .Select(g => new { g.Key.AssetId, g.Key.AssetName, RequestCount = g.Count() }),
            AverageFeedback = feedback.Count > 0 ? (double?)Math.Round(feedback.Average(f => f.Rating), 2) : null,
            FeedbackCount = feedback.Count,
            Priorities = requests.GroupBy(r => r.Priority).OrderBy(g => g.Key).Select(g => new { Priority = g.Key, Count = g.Count() }),
            MonthlyCompletions = completed.GroupBy(w => w.EndTime!.Value.ToString("yyyy-MM")).OrderBy(g => g.Key).Select(g => new { Month = g.Key, Count = g.Count() }),
            Technicians = jobs.GroupBy(w => new { w.TechnicianId, w.Technician }).Select(g => new { g.Key.TechnicianId, Name = g.Key.Technician, ActiveJobs = g.Count(w => w.Status != ServiceStatus.Completed && w.Status != ServiceStatus.Closed), CompletedJobs = completed.Count(w => w.TechnicianId == g.Key.TechnicianId), AverageRating = feedback.Any(f => f.TechnicianId == g.Key.TechnicianId) ? (double?)Math.Round(feedback.Where(f => f.TechnicianId == g.Key.TechnicianId).Average(f => f.Rating), 2) : null }),
            StockMovements = movement?.GroupBy(t => new { Month = t.TransactionDate.ToString("yyyy-MM"), Type = t.TransactionType }).OrderBy(g => g.Key.Month).Select(g => new { g.Key.Month, g.Key.Type, Quantity = g.Sum(t => (long)t.Quantity) }),
            LowStock = inventory ? await db.SpareParts.AsNoTracking().Where(p => p.QuantityInStock <= p.LowStockLevel).Select(p => new { p.PartName, p.QuantityInStock, p.LowStockLevel }).ToListAsync() : null
        });
    }
}
