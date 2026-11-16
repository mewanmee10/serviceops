using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Data;
namespace ServiceOps.Api.Authorization;

public sealed record PermissionRequirement(string Name) : IAuthorizationRequirement;
public sealed class PermissionHandler(AppDbContext db) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return;
        if (await db.Users.AnyAsync(u => u.UserId == id && u.AccountStatus == "Active" && u.Role.RolePermissions.Any(rp => rp.Permission.PermissionName == requirement.Name))) context.Succeed(requirement);
    }
}
