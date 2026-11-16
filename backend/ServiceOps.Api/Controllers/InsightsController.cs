using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Data;
using ServiceOps.Api.Enums;
using ServiceOps.Api.Services.Interfaces;
using ServiceOps.Api.Services.Operations;
namespace ServiceOps.Api.Controllers;

[ApiController, Route("api"), Authorize, TypeFilter(typeof(AccountApiExceptionFilter))]
public class InsightsController(AppDbContext db, OperationsAccess access) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var inventory = User.IsInRole("Administrator") || User.IsInRole("Inventory Staff");
        return Ok(new
        {
            OpenRequests = await access.Requests.CountAsync(r => r.Status != ServiceStatus.Completed && r.Status != ServiceStatus.Closed),
            ActiveJobs = await access.Jobs.CountAsync(w => w.Status != ServiceStatus.Completed && w.Status != ServiceStatus.Closed),
            CompletedJobs = await access.Jobs.CountAsync(w => w.Status == ServiceStatus.Completed || w.Status == ServiceStatus.Closed),
            OverdueJobs = await access.Jobs.CountAsync(w => w.ScheduledVisit < DateTime.UtcNow && w.Status != ServiceStatus.Completed && w.Status != ServiceStatus.Closed),
            LowStockParts = inventory ? (int?)await db.SpareParts.CountAsync(p => p.QuantityInStock <= p.LowStockLevel) : null
            , Users = User.IsInRole("Administrator") ? (int?)await db.Users.CountAsync() : null
            , Technicians = access.Staff ? (int?)await db.Technicians.CountAsync() : null
            , InventoryParts = User.IsInRole("Administrator") ? (int?)await db.SpareParts.CountAsync() : null
            , RecentActivities = User.IsInRole("Administrator") ? (int?)await db.AuditLogs.CountAsync(a => a.CreatedAt >= DateTime.UtcNow.AddDays(-1)) : null
        });
    }
    [HttpGet("administration/monitoring"), Authorize(Roles = "Administrator", Policy = "permission:audit.read")]
    public async Task<IActionResult> Monitoring()
    {
        var now = DateTime.UtcNow;
        return Ok(new
        {
            CheckedAt = now, DatabaseReachable = await db.Database.CanConnectAsync(),
            ActiveAccounts = await db.Users.CountAsync(u => u.AccountStatus == "Active"),
            InactiveAccounts = await db.Users.CountAsync(u => u.AccountStatus != "Active"),
            UnscheduledJobs = await db.WorkOrders.CountAsync(w => w.ScheduledVisit == null && w.Status < ServiceStatus.Completed),
            OverdueJobs = await db.WorkOrders.CountAsync(w => w.ScheduledVisit < now && w.Status < ServiceStatus.Completed),
            RecentActivity = await db.AuditLogs.AsNoTracking().OrderByDescending(a => a.AuditLogId).Take(20)
                .Select(a => new { a.AuditLogId, a.Action, a.EntityType, a.EntityId, a.CreatedAt }).ToListAsync()
        });
    }
    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(bool? isRead, string? search) => Ok(await db.Notifications.AsNoTracking().Where(n => n.UserId == access.UserId && (isRead == null || n.IsRead == isRead) && (search == null || n.Message.Contains(search))).OrderByDescending(n => n.CreatedAt).Take(100).Select(n => new { n.NotificationId, n.Message, n.CreatedAt, n.IsRead }).ToListAsync());
    [HttpGet("notifications/unread-count")]
    public async Task<IActionResult> Unread() => Ok(new { Count = await db.Notifications.CountAsync(n => n.UserId == access.UserId && !n.IsRead) });
    public record NotificationState(bool IsRead);
    [HttpPatch("notifications/{id:int}/state")]
    public async Task<IActionResult> State(int id, NotificationState input) { var n = await db.Notifications.SingleOrDefaultAsync(n => n.NotificationId == id && n.UserId == access.UserId); if (n == null) return NotFound(); n.IsRead = input.IsRead; await db.SaveChangesAsync(); return NoContent(); }
    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> ReadAll()
    {
        var changed = await db.Notifications.Where(n => n.UserId == access.UserId && !n.IsRead)
            .ExecuteUpdateAsync(update => update.SetProperty(n => n.IsRead, true));
        return Ok(new { Updated = changed });
    }
    [HttpPatch("notifications/{id:int}/read")]
    public async Task<IActionResult> Read(int id) { var n = await db.Notifications.SingleOrDefaultAsync(n => n.NotificationId == id && n.UserId == access.UserId); if (n == null) return NotFound(); n.IsRead = true; await db.SaveChangesAsync(); return NoContent(); }
    [HttpGet("reports/work-orders/{id:int}"), Authorize(Policy="permission:reports.read")]
    public async Task<IActionResult> Report(int id)
    { var w = await access.Jobs.Where(w => w.WorkOrderId == id && (w.Status == ServiceStatus.Completed || w.Status == ServiceStatus.Closed)).Select(w => new { w.WorkOrderId, w.ServiceRequestId, Customer = w.ServiceRequest.Customer.CustomerName, Site = w.ServiceRequest.CustomerSite.SiteName, Technician = w.Technician.User.FullName, w.Diagnosis, w.WorkPerformed, w.StartTime, w.EndTime, w.Status }).SingleOrDefaultAsync(); if (w == null) return NotFound(); return File(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(w), "application/json", $"service-report-{id}.json"); }
}
