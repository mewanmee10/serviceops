using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Data;
using ServiceOps.Api.Enums;
using ServiceOps.Api.Models;
namespace ServiceOps.Api.Services.Notifications;

// Polling is sufficient for the academic single-database deployment; no SignalR claim.
public sealed class OverdueNotificationService(IServiceScopeFactory scopes, ILogger<OverdueNotificationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await GenerateAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Overdue notification scan failed; retrying at the next interval."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public static async Task GenerateAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Serialize concurrent application instances to prevent duplicate alerts.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734102027)", cancellationToken);
        var jobs = await db.WorkOrders.AsNoTracking()
            .Where(w => w.ScheduledVisit < DateTime.UtcNow && w.Status != ServiceStatus.Completed && w.Status != ServiceStatus.Closed)
            .Select(w => new { w.WorkOrderId, w.ScheduledVisit, TechnicianUserId = w.Technician.UserId }).ToListAsync(cancellationToken);
        var managers = await db.Users.AsNoTracking().Where(u => u.AccountStatus == "Active" && u.Role.RoleName == "Service Manager")
            .Select(u => u.UserId).ToListAsync(cancellationToken);
        foreach (var job in jobs)
        {
            var message = $"Work order #{job.WorkOrderId} is overdue for its scheduled visit ({job.ScheduledVisit:yyyy-MM-dd HH:mm} UTC).";
            foreach (var userId in managers.Append(job.TechnicianUserId).Distinct())
                if (!await db.Notifications.AnyAsync(n => n.UserId == userId && n.Message == message, cancellationToken))
                    db.Notifications.Add(new Notification { UserId = userId, Message = message });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
