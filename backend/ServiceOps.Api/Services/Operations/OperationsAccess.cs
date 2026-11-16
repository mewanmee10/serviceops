using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Authorization;
using ServiceOps.Api.Data;
using ServiceOps.Api.Models;
namespace ServiceOps.Api.Services.Operations;

public sealed class OperationsAccess(AppDbContext db, IHttpContextAccessor accessor)
{
    public ClaimsPrincipal User => accessor.HttpContext!.User;
    public int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public bool Staff => User.IsInRole(ServiceOpsRoles.Administrator) || User.IsInRole(ServiceOpsRoles.ServiceManager);
    public IQueryable<Customer> Customers => db.Customers.Where(c => Staff || (User.IsInRole(ServiceOpsRoles.Customer) && c.UserId == UserId));
    public IQueryable<ServiceRequest> Requests => db.ServiceRequests.Where(r => Staff || (User.IsInRole(ServiceOpsRoles.Customer) && r.Customer.UserId == UserId) ||
        (User.IsInRole(ServiceOpsRoles.Technician) && r.WorkOrder != null && r.WorkOrder.Technician.UserId == UserId));
    public IQueryable<WorkOrder> Jobs => db.WorkOrders.Where(w => Staff || (User.IsInRole(ServiceOpsRoles.Technician) && w.Technician.UserId == UserId) || (User.IsInRole(ServiceOpsRoles.Customer) && w.ServiceRequest.Customer.UserId == UserId));
    public void Notify(int userId, string message) => db.Notifications.Add(new Notification { UserId = userId, Message = message });
}
