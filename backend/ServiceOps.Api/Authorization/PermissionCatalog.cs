using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Data;
using ServiceOps.Api.Models;
namespace ServiceOps.Api.Authorization;

public static class PermissionCatalog
{
    public static readonly Dictionary<string, string[]> Defaults = new()
    {
        ["users.manage"] = [ServiceOpsRoles.Administrator],
        ["permissions.manage"] = [ServiceOpsRoles.Administrator],
        ["audit.read"] = [ServiceOpsRoles.Administrator],
        ["inventory.manage"] = [ServiceOpsRoles.Administrator, ServiceOpsRoles.InventoryStaff],
        ["evidence.upload"] = [ServiceOpsRoles.Administrator, ServiceOpsRoles.ServiceManager, ServiceOpsRoles.Technician, ServiceOpsRoles.Customer],
        ["reports.read"] = [ServiceOpsRoles.Administrator, ServiceOpsRoles.ServiceManager, ServiceOpsRoles.Technician, ServiceOpsRoles.Customer],
        ["analytics.read"] = ServiceOpsRoles.All
    };
    public static readonly string[] EssentialAdministrator = ["users.manage", "permissions.manage", "audit.read"];
    public static async Task InitializeAsync(AppDbContext db)
    {
        using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Permissions\", \"RolePermissions\" IN SHARE ROW EXCLUSIVE MODE");
        var firstInstall = !await db.Permissions.AnyAsync(p => p.PermissionName == "permissions.manage");
        var roles = await db.Roles.Where(r => ServiceOpsRoles.All.Contains(r.RoleName)).ToListAsync();
        foreach (var pair in Defaults)
        {
            var permission = await db.Permissions.SingleOrDefaultAsync(p => p.PermissionName == pair.Key);
            if (permission == null) { permission = new Permission { PermissionName = pair.Key, Description = $"ServiceOps {pair.Key} capability" }; db.Permissions.Add(permission); await db.SaveChangesAsync(); }
            if (firstInstall)
                foreach (var role in roles.Where(r => pair.Value.Contains(r.RoleName)))
                    if (!await db.RolePermissions.AnyAsync(rp => rp.RoleId == role.RoleId && rp.PermissionId == permission.PermissionId)) db.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = permission.PermissionId });
            await db.SaveChangesAsync();
        }
        // Essential administrator access is an invariant, not an optional grant.
        foreach (var role in roles.Where(r => r.RoleName == ServiceOpsRoles.Administrator))
            foreach (var name in EssentialAdministrator)
            { var id = await db.Permissions.Where(p => p.PermissionName == name).Select(p => p.PermissionId).SingleAsync(); if (!await db.RolePermissions.AnyAsync(p => p.RoleId == role.RoleId && p.PermissionId == id)) db.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = id }); }
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
}
