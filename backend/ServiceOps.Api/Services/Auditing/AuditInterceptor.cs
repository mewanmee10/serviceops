using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Security.Claims;
using ServiceOps.Api.Models;
namespace ServiceOps.Api.Services.Auditing;

public sealed class AuditInterceptor(IHttpContextAccessor accessor) : SaveChangesInterceptor
{
    private readonly List<(PropertyEntry Key, AuditLog Audit)> generatedKeys = [];

    private void Capture(DbContext? context)
    {
        if (context == null || !int.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return;
        var changes = context.ChangeTracker.Entries().Where(e => e.Entity is not AuditLog && e.Entity is not Notification && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        foreach (var entry in changes)
        {
            string action = entry.State switch { EntityState.Added => "Created", EntityState.Deleted => "Deleted", _ => "Updated" };
            bool Changed(string name) => entry.State == EntityState.Modified && entry.Properties.Any(p => p.Metadata.Name == name && p.IsModified && !Equals(p.OriginalValue, p.CurrentValue));
            if (entry.Entity is User) action = entry.State == EntityState.Added ? "User created" : Changed("PasswordHash") ? "Password changed" : Changed("AccountStatus") ? "Account status changed" : Changed("RoleId") ? "User role changed" : "User updated";
            if (entry.Entity is WorkOrder) action = entry.State == EntityState.Added ? "Technician assigned" : Changed("Status") ? "Work-order status transitioned" : "Assignment or job details updated";
            if (entry.Entity is ServiceRequest) action = Changed("Priority") ? "Request priority changed" : entry.State == EntityState.Added ? "Service request submitted" : "Service request updated";
            if (entry.Entity is InventoryTransaction) action = "Stock movement recorded";
            if (entry.Entity is CustomerConfirmation) action = "Completion confirmed";
            if (entry.Entity is RolePermission) action = entry.State == EntityState.Deleted ? "Role permission revoked" : "Role permission granted";
            if (entry.Entity is Attachment) action = "Evidence uploaded";
            var keys = entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).ToList();
            int? id = keys.Count == 1 && keys[0].CurrentValue is int value && value > 0 ? value : null;
            var audit = new AuditLog { UserId = actor, Action = action, EntityType = entry.Metadata.ClrType.Name, EntityId = id };
            context.Set<AuditLog>().Add(audit);
            if (entry.State == EntityState.Added && keys.Count == 1 && keys[0].Metadata.ClrType == typeof(int))
                generatedKeys.Add((keys[0], audit));
        }
    }
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) { Capture(eventData.Context); return result; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) { Capture(eventData.Context); return ValueTask.FromResult(result); }

    private List<AuditLog> ResolveKeys()
    {
        var changed = new List<AuditLog>();
        foreach (var (key, audit) in generatedKeys)
            if (key.CurrentValue is int id && id > 0 && audit.EntityId != id)
            {
                audit.EntityId = id;
                changed.Add(audit);
            }
        generatedKeys.Clear();
        return changed;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context == null) return result;
        foreach (var audit in ResolveKeys())
            eventData.Context.Database.ExecuteSqlInterpolated($"UPDATE \"AuditLogs\" SET \"EntityId\" = {audit.EntityId} WHERE \"AuditLogId\" = {audit.AuditLogId}");
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context == null) return result;
        foreach (var audit in ResolveKeys())
            await eventData.Context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"AuditLogs\" SET \"EntityId\" = {audit.EntityId} WHERE \"AuditLogId\" = {audit.AuditLogId}", cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => generatedKeys.Clear();
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        generatedKeys.Clear();
        return Task.CompletedTask;
    }
}
