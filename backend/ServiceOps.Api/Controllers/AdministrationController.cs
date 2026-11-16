using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Authorization;
using ServiceOps.Api.Data;
using ServiceOps.Api.Models;
using ServiceOps.Api.Services.Interfaces;
namespace ServiceOps.Api.Controllers;

[ApiController, Route("api/administration"), Authorize(Roles = "Administrator"), TypeFilter(typeof(AccountApiExceptionFilter))]
public sealed class AdministrationController(AppDbContext db) : ControllerBase
{
    public record GrantsRequest([Required] int[] PermissionIds);
    [HttpGet("permissions"), Authorize(Policy = "permission:permissions.manage")]
    public async Task<IActionResult> Permissions() => Ok(await db.Permissions.AsNoTracking().OrderBy(p => p.PermissionName).Select(p => new { p.PermissionId, p.PermissionName, p.Description }).ToListAsync());
    [HttpGet("roles"), Authorize(Policy = "permission:permissions.manage")]
    public async Task<IActionResult> Roles() => Ok(await db.Roles.AsNoTracking().Where(r => ServiceOpsRoles.All.Contains(r.RoleName)).OrderBy(r => r.RoleId).Select(r => new { r.RoleId, r.RoleName, PermissionIds = r.RolePermissions.Select(p => p.PermissionId) }).ToListAsync());
    [HttpPut("roles/{id:int}/permissions"), Authorize(Policy = "permission:permissions.manage")]
    public async Task<IActionResult> Grants(int id, GrantsRequest input)
    {
        if (input.PermissionIds.Length > 100 || input.PermissionIds.Distinct().Count() != input.PermissionIds.Length) return BadRequest(new { message = "Provide distinct valid permission IDs." });
        using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Permissions\", \"RolePermissions\" IN SHARE ROW EXCLUSIVE MODE");
        var role = await db.Roles.Include(r => r.RolePermissions).SingleOrDefaultAsync(r => r.RoleId == id && ServiceOpsRoles.All.Contains(r.RoleName)); if (role == null) return NotFound();
        var permissions = await db.Permissions.Where(p => input.PermissionIds.Contains(p.PermissionId)).ToListAsync(); if (permissions.Count != input.PermissionIds.Length) return BadRequest(new { message = "Unknown permission." });
        if (role.RoleName == ServiceOpsRoles.Administrator && PermissionCatalog.EssentialAdministrator.Any(name => permissions.All(p => p.PermissionName != name))) return Conflict(new { message = "Administrator's user, permission and audit capabilities cannot be removed." });
        db.RolePermissions.RemoveRange(role.RolePermissions.Where(p => !input.PermissionIds.Contains(p.PermissionId)));
        foreach (var idPermission in input.PermissionIds.Where(idPermission => role.RolePermissions.All(p => p.PermissionId != idPermission))) db.RolePermissions.Add(new RolePermission { RoleId = id, PermissionId = idPermission });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return NoContent();
    }
    [HttpGet("audit/{id:int}"), Authorize(Policy = "permission:audit.read")]
    public async Task<IActionResult> AuditDetails(int id)
    {
        var item = await db.AuditLogs.AsNoTracking().Where(a => a.AuditLogId == id)
            .Select(a => new { a.AuditLogId, Actor = a.User.FullName, ActorRole = a.User.Role.RoleName, a.Action, a.EntityType, a.EntityId, a.CreatedAt }).SingleOrDefaultAsync();
        return item == null ? NotFound() : Ok(item);
    }
    [HttpGet("audit"), Authorize(Policy = "permission:audit.read")]
    public async Task<IActionResult> Audit(string? entityType, string? action, int? userId, DateTimeOffset? from, DateTimeOffset? to, int page = 1, int pageSize = 25, string? search = null, string sort = "desc")
    {
        if (page < 1 || page > 1000000 || pageSize < 1 || pageSize > 100 || from > to || (sort != "asc" && sort != "desc") || search?.Length > 200) return BadRequest();
        var query = db.AuditLogs.AsNoTracking().Where(a => (entityType == null || a.EntityType == entityType) && (action == null || a.Action.Contains(action)) && (userId == null || a.UserId == userId) && (from == null || a.CreatedAt >= from.Value.UtcDateTime) && (to == null || a.CreatedAt <= to.Value.UtcDateTime));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(a => a.User.FullName.Contains(search) || a.Action.Contains(search) || a.EntityType.Contains(search));
        var ordered = sort == "asc" ? query.OrderBy(a => a.CreatedAt).ThenBy(a => a.AuditLogId) : query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AuditLogId);
        return Ok(new { Total = await query.CountAsync(), Page = page, PageSize = pageSize, Items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(a => new { a.AuditLogId, a.UserId, Actor = a.User.FullName, ActorRole = a.User.Role.RoleName, a.Action, a.EntityType, a.EntityId, a.CreatedAt }).ToListAsync() });
    }
}
