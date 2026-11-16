using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Enums;
using ServiceOps.Api.Services.Interfaces;
using ServiceOps.Api.Services.Operations;
using ServiceOps.Api.Services.Reporting;
namespace ServiceOps.Api.Controllers;

[Authorize(Policy = "permission:reports.read")]
[ApiController, Route("api/reports"), Authorize(Roles = "Administrator,Service Manager,Technician,Customer"), TypeFilter(typeof(AccountApiExceptionFilter))]
public sealed class ReportsController(OperationsAccess access, ServiceReportPdf reports) : ControllerBase
{
    [HttpGet("work-orders/{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id)
    {
        var work = await access.Jobs.AsNoTracking().AsSplitQuery()
            .Include(w => w.ServiceRequest).ThenInclude(r => r.Customer).ThenInclude(c => c.User)
            .Include(w => w.ServiceRequest).ThenInclude(r => r.CustomerSite)
            .Include(w => w.ServiceRequest).ThenInclude(r => r.Asset)
            .Include(w => w.Technician).ThenInclude(t => t.User)
            .Include(w => w.CustomerConfirmation).Include(w => w.InventoryTransactions).ThenInclude(t => t.SparePart)
            .SingleOrDefaultAsync(w => w.WorkOrderId == id && (w.Status == ServiceStatus.Completed || w.Status == ServiceStatus.Closed));
        if (work == null) return NotFound(); Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(reports.Create(work), "application/pdf", $"ServiceOps-SO-{id:000000}.pdf");
    }
}
