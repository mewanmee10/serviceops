using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Data;
using ServiceOps.Api.Enums;
using ServiceOps.Api.Services.Interfaces;
using ServiceOps.Api.Services.Operations;

namespace ServiceOps.Api.Controllers;

[ApiController, Route("api/locations"), Authorize(Roles = "Administrator,Service Manager,Technician,Customer"), TypeFilter(typeof(AccountApiExceptionFilter))]
public sealed class LocationsController(AppDbContext db, OperationsAccess access) : ControllerBase
{
    public record Pin(string Id, int SiteId, string Name, string Address, decimal? Latitude, decimal? Longitude,
        string LocationType, int? WorkOrderId, string? TechnicianName);

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var technician = User.IsInRole("Technician");
        var customer = User.IsInRole("Customer");
        var pins = new List<Pin>();
        if (!technician)
            pins.AddRange(await db.CustomerSites.AsNoTracking()
                .Where(s => access.Customers.Any(c => c.CustomerId == s.CustomerId))
                .OrderBy(s => s.SiteId)
                .Select(s => new Pin("site-" + s.SiteId, s.SiteId, s.SiteName, s.AddressLine,
                    s.Latitude, s.Longitude, "Customer Service Location", null, null)).ToListAsync());
        if (!customer)
            pins.AddRange(await access.Jobs.AsNoTracking()
                .Where(w => w.Status != ServiceStatus.Completed && w.Status != ServiceStatus.Closed)
                .OrderBy(w => w.WorkOrderId)
                .Select(w => new Pin("job-" + w.WorkOrderId, w.ServiceRequest.SiteId,
                    w.ServiceRequest.CustomerSite.SiteName, w.ServiceRequest.CustomerSite.AddressLine,
                    w.ServiceRequest.CustomerSite.Latitude, w.ServiceRequest.CustomerSite.Longitude,
                    "Assigned Service Location", w.WorkOrderId, w.Technician.User.FullName)).ToListAsync());
        return Ok(pins);
    }
}
