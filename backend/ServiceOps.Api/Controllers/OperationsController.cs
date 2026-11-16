using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Data;
using ServiceOps.Api.DTOs.Operations;
using ServiceOps.Api.Enums;
using ServiceOps.Api.Models;
using ServiceOps.Api.Services.Interfaces;
using ServiceOps.Api.Services.Operations;
namespace ServiceOps.Api.Controllers;

[ApiController, Route("api"), Authorize(Roles = "Administrator,Service Manager,Technician,Customer"), TypeFilter(typeof(AccountApiExceptionFilter))]
public class OperationsController(AppDbContext db, OperationsAccess access) : ControllerBase
{
    [HttpGet("requests")]
    public async Task<IActionResult> Requests(ServiceStatus? status, Priority? priority) => Ok(await access.Requests.AsNoTracking()
        .Where(r => (status == null || r.Status == status) && (priority == null || r.Priority == priority)).OrderByDescending(r => r.CreatedAt)
        .Select(r => new { r.ServiceRequestId, r.CustomerId, r.SiteId, r.AssetId, r.IssueDescription, r.Category, r.Priority, r.Status, r.CreatedAt, r.UpdatedAt, SiteName = r.CustomerSite.SiteName, CustomerName = r.Customer.CustomerName, WorkOrderId = r.WorkOrder == null ? (int?)null : r.WorkOrder.WorkOrderId }).ToListAsync());
    [HttpGet("requests/{id:int}")]
    public async Task<IActionResult> RequestDetails(int id) { var r = await access.Requests.Where(r => r.ServiceRequestId == id).Select(r => new { r.ServiceRequestId, r.CustomerId, r.SiteId, r.AssetId, r.IssueDescription, r.Category, r.Priority, r.Status, r.CreatedAt, r.UpdatedAt }).SingleOrDefaultAsync(); return r == null ? NotFound() : Ok(r); }
    [HttpPost("requests"), Authorize(Roles = "Administrator,Service Manager,Customer")]
    public async Task<IActionResult> CreateRequest(ServiceRequestInput input)
    {
        var site = await db.CustomerSites.SingleOrDefaultAsync(s => s.SiteId == input.SiteId && (access.Staff || s.Customer.UserId == access.UserId)); if (site == null) return NotFound();
        if (input.AssetId != null && !await db.Assets.AnyAsync(a => a.AssetId == input.AssetId && a.SiteId == site.SiteId)) return BadRequest(new { message = "Asset must belong to the selected site." });
        var r = new ServiceRequest { CustomerId = site.CustomerId, SiteId = site.SiteId, AssetId = input.AssetId, IssueDescription = input.IssueDescription.Trim(), Category = input.Category.Trim(), Priority = Priority.Medium }; db.ServiceRequests.Add(r);
        var managers = await db.Users.Where(u => u.Role.RoleName == "Service Manager" && u.AccountStatus == "Active").Select(u => u.UserId).ToListAsync(); foreach (var id in managers) access.Notify(id, "A new service request needs review.");
        await db.SaveChangesAsync(); return Created($"/api/requests/{r.ServiceRequestId}", new { r.ServiceRequestId, r.Status });
    }
    [HttpPut("requests/{id:int}"), Authorize(Roles = "Administrator,Service Manager")]
    public async Task<IActionResult> EditRequest(int id, ServiceRequestEdit input)
    {
        if (string.IsNullOrWhiteSpace(input.IssueDescription) || string.IsNullOrWhiteSpace(input.Category))
            return BadRequest(new { message = "Issue description and category are required." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"ServiceRequests\" IN SHARE ROW EXCLUSIVE MODE");
        var request = await db.ServiceRequests.SingleOrDefaultAsync(r => r.ServiceRequestId == id);
        if (request == null) return NotFound();
        if (request.Status != ServiceStatus.New) return Conflict(new { message = "Issue details can only be edited before assignment." });
        request.IssueDescription = input.IssueDescription.Trim();
        request.Category = input.Category.Trim();
        request.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }

    [HttpPatch("requests/{id:int}/priority"), Authorize(Roles = "Administrator,Service Manager")]
    public async Task<IActionResult> PriorityUpdate(int id, PriorityRequest input)
    { var r = await db.ServiceRequests.SingleOrDefaultAsync(r => r.ServiceRequestId == id); if (r == null) return NotFound(); if (r.Status is ServiceStatus.Completed or ServiceStatus.Closed) return Conflict(new { message = "Completed requests cannot be reprioritized." }); r.Priority = input.Priority; r.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return NoContent(); }

    [HttpGet("technicians"), Authorize(Roles = "Administrator,Service Manager,Technician")]
    public async Task<IActionResult> Technicians() => Ok(await db.Technicians.Where(t => access.Staff || t.UserId == access.UserId)
        .Select(t => new { t.TechnicianId, t.UserId, FullName = t.User.FullName, t.Skill, t.Availability, CurrentWorkload = t.WorkOrders.Count(w => w.Status != ServiceStatus.Completed && w.Status != ServiceStatus.Closed), AccountStatus = t.User.AccountStatus }).ToListAsync());
    [HttpPut("technicians/{id:int}"), Authorize(Roles = "Administrator,Service Manager")]
    public async Task<IActionResult> UpdateTechnician(int id, TechnicianRequest input)
    { var t = await db.Technicians.SingleOrDefaultAsync(t => t.TechnicianId == id); if (t == null) return NotFound(); if (t.UserId != input.UserId) return BadRequest(); t.Skill = input.Skill.Trim(); t.Availability = input.Availability; await db.SaveChangesAsync(); return NoContent(); }

    [HttpPost("requests/{id:int}/assign"), Authorize(Roles = "Administrator,Service Manager")]
    public async Task<IActionResult> Assign(int id, AssignmentRequest input)
    {
        if (input.ScheduledVisit is { } date && date < DateTimeOffset.UtcNow) return BadRequest(new { message = "Schedule must be a future UTC timestamp." });
        using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"ServiceRequests\", \"WorkOrders\", \"Technicians\" IN SHARE ROW EXCLUSIVE MODE");
        var r = await db.ServiceRequests.Include(r => r.Customer).SingleOrDefaultAsync(r => r.ServiceRequestId == id); if (r == null) return NotFound();
        if (r.Status != ServiceStatus.New || await db.WorkOrders.AnyAsync(w => w.ServiceRequestId == id)) return Conflict(new { message = "Request has already been assigned." });
        var t = await db.Technicians.Include(t => t.User).ThenInclude(u => u.Role).SingleOrDefaultAsync(t => t.TechnicianId == input.TechnicianId);
        if (t == null || !t.Availability || t.User.AccountStatus != "Active" || t.User.Role.RoleName != "Technician") return BadRequest(new { message = "Select an available active technician." });
        var w = new WorkOrder { ServiceRequestId = id, TechnicianId = t.TechnicianId, ScheduledVisit = input.ScheduledVisit?.UtcDateTime, JobNote = input.JobNote };
        w.JobStatusHistories.Add(new JobStatusHistory { Status = ServiceStatus.Assigned, Remarks = "Assigned by manager." }); db.WorkOrders.Add(w); t.CurrentWorkload = await db.WorkOrders.CountAsync(j => j.TechnicianId == t.TechnicianId && j.Status != ServiceStatus.Completed && j.Status != ServiceStatus.Closed) + 1; r.Status = ServiceStatus.Assigned; r.UpdatedAt = DateTime.UtcNow;
        access.Notify(t.UserId, "A new work order has been assigned to you."); access.Notify(r.Customer.UserId, "Your service request has been assigned."); await db.SaveChangesAsync(); await tx.CommitAsync(); return Created($"/api/work-orders/{w.WorkOrderId}", new { w.WorkOrderId, w.ServiceRequestId, w.TechnicianId, w.Status });
    }
    [HttpPatch("work-orders/{id:int}/assignment"), Authorize(Roles = "Administrator,Service Manager")]
    public async Task<IActionResult> Reassign(int id, AssignmentRequest input)
    {
        if (input.ScheduledVisit is { } date && date < DateTimeOffset.UtcNow)
            return BadRequest(new { message = "Schedule must be in the future." });
        using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"ServiceRequests\", \"WorkOrders\", \"Technicians\" IN SHARE ROW EXCLUSIVE MODE");
        var job = await db.WorkOrders.Include(w => w.Technician).SingleOrDefaultAsync(w => w.WorkOrderId == id);
        if (job == null) return NotFound();
        if (job.Status != ServiceStatus.Assigned) return Conflict(new { message = "Reassignment is allowed only before work starts." });
        var technician = await db.Technicians.Include(t => t.User).ThenInclude(u => u.Role).SingleOrDefaultAsync(t => t.TechnicianId == input.TechnicianId);
        if (technician == null || !technician.Availability || technician.User.AccountStatus != "Active" || technician.User.Role.RoleName != "Technician")
            return BadRequest(new { message = "Select an available active technician." });
        if (job.TechnicianId != technician.TechnicianId)
        {
            job.Technician.CurrentWorkload = await db.WorkOrders.CountAsync(w => w.TechnicianId == job.TechnicianId && w.WorkOrderId != id && w.Status != ServiceStatus.Completed && w.Status != ServiceStatus.Closed);
            technician.CurrentWorkload = await db.WorkOrders.CountAsync(w => w.TechnicianId == technician.TechnicianId && w.Status != ServiceStatus.Completed && w.Status != ServiceStatus.Closed) + 1;
            access.Notify(job.Technician.UserId, $"Work order #{id} has been reassigned.");
            job.TechnicianId = technician.TechnicianId;
        }
        job.ScheduledVisit = input.ScheduledVisit?.UtcDateTime;
        job.JobNote = input.JobNote;
        access.Notify(technician.UserId, $"Work order #{id} assignment or schedule has been updated.");
        db.JobStatusHistories.Add(new JobStatusHistory { WorkOrderId = id, Status = ServiceStatus.Assigned, Remarks = "Assignment or schedule updated by manager." });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpGet("work-orders")]
    public async Task<IActionResult> Jobs() => Ok(await access.Jobs.AsNoTracking().OrderByDescending(w => w.WorkOrderId)
        .Select(w => new { w.WorkOrderId, w.ServiceRequestId, w.TechnicianId, w.ScheduledVisit, w.Status, w.StartTime, w.EndTime, w.Diagnosis, w.WorkPerformed, w.JobNote, TechnicianName = w.Technician.User.FullName, IssueDescription = w.ServiceRequest.IssueDescription, SiteName = w.ServiceRequest.CustomerSite.SiteName }).ToListAsync());
    [HttpGet("work-orders/{id:int}/parts-catalog"), Authorize(Roles = "Technician")]
    public async Task<IActionResult> PartsCatalog(int id)
    {
        if (!await access.Jobs.AnyAsync(w => w.WorkOrderId == id)) return NotFound();
        return Ok(await db.SpareParts.AsNoTracking().OrderBy(p => p.PartName)
            .Select(p => new { p.SparePartId, p.PartName }).ToListAsync());
    }
    [HttpPost("work-orders/{id:int}/parts-handoff"), Authorize(Roles = "Technician")]
    public async Task<IActionResult> PartsHandoff(int id, PartsHandoffRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.Remarks)) return BadRequest(new { message = "Describe the actual usage for Inventory Staff." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"WorkOrders\", \"SpareParts\" IN SHARE ROW EXCLUSIVE MODE");
        var job = await access.Jobs.SingleOrDefaultAsync(w => w.WorkOrderId == id);
        if (job == null) return NotFound();
        if (job.Status != ServiceStatus.InProgress) return Conflict(new { message = "Usage handoff requires an in-progress assigned job." });
        var part = await db.SpareParts.SingleOrDefaultAsync(p => p.SparePartId == input.SparePartId);
        if (part == null) return BadRequest(new { message = "Select a valid spare part." });
        var recipients = await db.Users.Where(u => u.AccountStatus == "Active" && u.Role.RoleName == "Inventory Staff")
            .Select(u => u.UserId).ToListAsync();
        if (recipients.Count == 0) return Conflict(new { message = "No active Inventory Staff account can receive this handoff." });
        var note = $"Parts usage declaration: part #{part.SparePartId} ({part.PartName}), {input.Quantity} units. {input.Remarks.Trim()}";
        // A declaration is durable job evidence, not a stock movement or verified consumption.
        var history = new JobStatusHistory { WorkOrderId = id, Status = job.Status, Remarks = note };
        db.JobStatusHistories.Add(history);
        foreach (var recipient in recipients) access.Notify(recipient, $"Work order #{id} — {note} Inventory reconciliation required.");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { history.StatusHistoryId, WorkOrderId = id, Message = "Usage recorded and sent to Inventory Staff; stock is unchanged." });
    }
    [HttpGet("work-orders/{id:int}")]
    public async Task<IActionResult> Job(int id)
    { var w = await access.Jobs.Where(w => w.WorkOrderId == id).Select(w => new { w.WorkOrderId, w.ServiceRequestId, w.TechnicianId, w.ScheduledVisit, w.Status, w.Diagnosis, w.WorkPerformed, w.StartTime, w.EndTime, w.JobNote, Parts = w.InventoryTransactions.OrderBy(t => t.TransactionDate).Select(t => new { t.TransactionId, t.SparePartId, t.SparePart.PartName, t.TransactionType, t.Quantity, t.TransactionDate }), Checklist = w.ChecklistItems.Select(c => new { c.ChecklistItemId, c.Description, c.IsCompleted, c.CompletedAt }), History = w.JobStatusHistories.OrderBy(h => h.ChangedAt).Select(h => new { h.Status, h.ChangedAt, h.Remarks }), CustomerEmail = w.ServiceRequest.Customer.User.Email, CustomerPhone = w.ServiceRequest.Customer.User.PhoneNumber, CustomerName = w.ServiceRequest.Customer.CustomerName, MapsUrl = w.ServiceRequest.CustomerSite.MapsUrl, SiteCity = w.ServiceRequest.CustomerSite.City, SiteProvince = w.ServiceRequest.CustomerSite.Province, SiteName = w.ServiceRequest.CustomerSite.SiteName, AddressLine = w.ServiceRequest.CustomerSite.AddressLine, AssetName = w.ServiceRequest.Asset == null ? null : w.ServiceRequest.Asset.AssetName, SerialNumber = w.ServiceRequest.Asset == null ? null : w.ServiceRequest.Asset.SerialNumber, InstallationDate = w.ServiceRequest.Asset == null ? (DateTime?)null : w.ServiceRequest.Asset.InstallationDate, WarrantyInformation = w.ServiceRequest.Asset == null ? null : w.ServiceRequest.Asset.WarrantyInformation, IssueDescription = w.ServiceRequest.IssueDescription, Confirmed = w.CustomerConfirmation != null && w.CustomerConfirmation.IsConfirmed }).SingleOrDefaultAsync(); return w == null ? NotFound() : Ok(w); }
    [HttpPatch("work-orders/{id:int}"), Authorize(Roles = "Technician")]
    public async Task<IActionResult> UpdateJob(int id, JobUpdateRequest input)
    {
        using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"WorkOrders\", \"ServiceRequests\", \"Technicians\", \"ServiceChecklistItems\" IN SHARE ROW EXCLUSIVE MODE");
        var w = await db.WorkOrders.Include(w => w.ServiceRequest).ThenInclude(r => r.Customer).Include(w => w.Technician).Include(w => w.ChecklistItems).SingleOrDefaultAsync(w => w.WorkOrderId == id && w.Technician.UserId == access.UserId); if (w == null) return NotFound();
        var valid = (w.Status == ServiceStatus.Assigned && input.Status == ServiceStatus.InProgress) || (w.Status == ServiceStatus.InProgress && (input.Status == ServiceStatus.OnHold || input.Status == ServiceStatus.Completed || input.Status == ServiceStatus.InProgress)) || (w.Status == ServiceStatus.OnHold && input.Status == ServiceStatus.InProgress);
        if (!valid) return Conflict(new { message = "Invalid job state transition." });
        if (input.Status == ServiceStatus.Completed && (string.IsNullOrWhiteSpace(input.Diagnosis) || string.IsNullOrWhiteSpace(input.WorkPerformed) || w.ChecklistItems.Any(c => !c.IsCompleted))) return BadRequest(new { message = "Diagnosis, work performed and all checklist items are required for completion." });
        if (input.JobNote != null) w.JobNote = input.JobNote.Trim();
        var now = DateTime.UtcNow; w.Diagnosis = input.Diagnosis ?? w.Diagnosis; w.WorkPerformed = input.WorkPerformed ?? w.WorkPerformed;
        if (w.StartTime == null && input.Status == ServiceStatus.InProgress) w.StartTime = now;
        if (input.Status == ServiceStatus.Completed) { w.EndTime = now; w.Technician.CurrentWorkload = await db.WorkOrders.CountAsync(j => j.TechnicianId == w.TechnicianId && j.WorkOrderId != id && j.Status != ServiceStatus.Completed && j.Status != ServiceStatus.Closed); if (w.ServiceRequest.AssetId is int asset) db.MaintenanceRecords.Add(new MaintenanceRecord { AssetId = asset, WorkOrderId = id, FaultDescription = w.Diagnosis, RepairDetails = w.WorkPerformed, ServiceDate = now }); }
        var managerIds = await db.Users.Where(u => u.AccountStatus == "Active" && u.Role.RoleName == "Service Manager").Select(u => u.UserId).ToListAsync();
        foreach (var managerId in managerIds) access.Notify(managerId, $"Work order #{id} is {input.Status}.");
        w.Status = input.Status; w.ServiceRequest.Status = input.Status; w.ServiceRequest.UpdatedAt = now; db.JobStatusHistories.Add(new JobStatusHistory { WorkOrderId = id, Status = input.Status, Remarks = "Updated by assigned technician." }); access.Notify(w.ServiceRequest.Customer.UserId, $"Work order #{id} is {input.Status}."); await db.SaveChangesAsync(); await tx.CommitAsync(); return NoContent();
    }
    [HttpPost("work-orders/{id:int}/checklist"), Authorize(Roles = "Administrator,Service Manager")]
    public async Task<IActionResult> AddChecklist(int id, ChecklistRequest input)
    { using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"WorkOrders\", \"ServiceChecklistItems\" IN SHARE ROW EXCLUSIVE MODE"); var w = await db.WorkOrders.SingleOrDefaultAsync(w => w.WorkOrderId == id); if (w == null) return NotFound(); if (w.Status is ServiceStatus.Completed or ServiceStatus.Closed) return Conflict(); var c = new ServiceChecklistItem { WorkOrderId = id, Description = input.Description.Trim() }; db.ServiceChecklistItems.Add(c); await db.SaveChangesAsync(); await tx.CommitAsync(); return Ok(new { c.ChecklistItemId, c.Description, c.IsCompleted }); }
    [HttpPatch("work-orders/{id:int}/checklist/{itemId:int}"), Authorize(Roles = "Technician")]
    public async Task<IActionResult> Checklist(int id, int itemId, ChecklistUpdate input)
    { using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"WorkOrders\", \"ServiceChecklistItems\" IN SHARE ROW EXCLUSIVE MODE"); var c = await db.ServiceChecklistItems.Include(c => c.WorkOrder).SingleOrDefaultAsync(c => c.ChecklistItemId == itemId && c.WorkOrderId == id && c.WorkOrder.Technician.UserId == access.UserId); if (c == null) return NotFound(); if (c.WorkOrder.Status != ServiceStatus.InProgress) return Conflict(new { message = "Checklist updates require an in-progress job." }); c.IsCompleted = input.IsCompleted; c.CompletedAt = input.IsCompleted ? DateTime.UtcNow : null; await db.SaveChangesAsync(); await tx.CommitAsync(); return NoContent(); }
    [HttpPost("work-orders/{id:int}/confirm"), Authorize(Roles = "Customer")]
    public async Task<IActionResult> Confirm(int id)
    { using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"WorkOrders\", \"ServiceRequests\" IN SHARE ROW EXCLUSIVE MODE"); var w = await db.WorkOrders.Include(w => w.ServiceRequest).SingleOrDefaultAsync(w => w.WorkOrderId == id && w.ServiceRequest.Customer.UserId == access.UserId); if (w == null) return NotFound(); if (w.Status != ServiceStatus.Completed) return Conflict(new { message = "Only completed work can be confirmed once." }); db.CustomerConfirmations.Add(new CustomerConfirmation { WorkOrderId = id, CustomerId = w.ServiceRequest.CustomerId, IsConfirmed = true, ConfirmedAt = DateTime.UtcNow }); w.Status = ServiceStatus.Closed; w.ServiceRequest.Status = ServiceStatus.Closed; w.ServiceRequest.UpdatedAt = DateTime.UtcNow; db.JobStatusHistories.Add(new JobStatusHistory { WorkOrderId = id, Status = ServiceStatus.Closed, Remarks = "Confirmed by customer." }); await db.SaveChangesAsync(); await tx.CommitAsync(); return NoContent(); }
    [HttpPost("work-orders/{id:int}/feedback"), Authorize(Roles = "Customer")]
    public async Task<IActionResult> Feedback(int id, FeedbackRequest input)
    { using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"WorkOrders\", \"CustomerFeedbacks\" IN SHARE ROW EXCLUSIVE MODE"); var w = await db.WorkOrders.Include(w => w.ServiceRequest).SingleOrDefaultAsync(w => w.WorkOrderId == id && w.ServiceRequest.Customer.UserId == access.UserId); if (w == null) return NotFound(); if (w.Status != ServiceStatus.Closed || await db.CustomerFeedbacks.AnyAsync(f => f.WorkOrderId == id)) return Conflict(new { message = "Feedback requires confirmed work and can be submitted once." }); db.CustomerFeedbacks.Add(new CustomerFeedback { WorkOrderId = id, CustomerId = w.ServiceRequest.CustomerId, Rating = input.Rating, Comments = input.Comments }); await db.SaveChangesAsync(); await tx.CommitAsync(); return NoContent(); }
    [HttpGet("maintenance")]
    public async Task<IActionResult> Maintenance(int? assetId) => Ok(await db.MaintenanceRecords.AsNoTracking().Where(m => (assetId == null || m.AssetId == assetId) && (access.Staff || access.Jobs.Any(w => w.ServiceRequest.AssetId == m.AssetId)))
        .OrderByDescending(m => m.ServiceDate).Select(m => new { m.MaintenanceRecordId, m.AssetId, m.WorkOrderId, m.FaultDescription, m.RepairDetails, m.ServiceDate, AssetName = m.Asset.AssetName,
            TechnicianName = m.WorkOrder.Technician.User.FullName, CustomerName = m.WorkOrder.ServiceRequest.Customer.CustomerName,
            Parts = m.WorkOrder.InventoryTransactions.Where(t => t.TransactionType == InventoryTransactionType.PartConsumption)
                .Select(t => new { t.SparePart.PartName, t.Quantity }),
            CanReadReport = access.Jobs.Any(w => w.WorkOrderId == m.WorkOrderId) }).ToListAsync());
}
