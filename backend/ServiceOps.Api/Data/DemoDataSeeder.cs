using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Authorization;
using ServiceOps.Api.DTOs.Users;
using ServiceOps.Api.Enums;
using ServiceOps.Api.Models;
using ServiceOps.Api.Services.Interfaces;

namespace ServiceOps.Api.Data;

/// <summary>Opt-in fictional data. Existing identities, passwords and progressed jobs are never reset.</summary>
public static class DemoDataSeeder
{
    private const string Marker = "ServiceOps development demo v1";
    private const long SeedLock = 734102026;
    private record Account(string Key, string Role, string Name, string Email);
    private static readonly Account[] Accounts =
    [
        new("Administrator", ServiceOpsRoles.Administrator, "Demonstration Administrator", "admin.demo@serviceops.local"),
        new("Manager", ServiceOpsRoles.ServiceManager, "Demonstration Service Manager", "manager.demo@serviceops.local"),
        new("Technician", ServiceOpsRoles.Technician, "Demonstration Technician", "technician.demo@serviceops.local"),
        new("Inventory", ServiceOpsRoles.InventoryStaff, "Demonstration Inventory Staff", "inventory.demo@serviceops.local"),
        new("Customer", ServiceOpsRoles.Customer, "Demonstration Customer", "customer.demo@serviceops.local")
    ];

    public static async Task SeedAsync(AppDbContext db, IConfiguration config,
        IHostEnvironment environment, IUserService users)
    {
        // Defense in depth: even a direct caller cannot seed outside Development.
        if (!environment.IsDevelopment() || !config.GetValue<bool>("SeedDemo:Enabled")) return;

        // A session lock serializes startup seeds while UserService owns each account transaction.
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock({SeedLock})");
        try
        {
            var passwords = new Dictionary<string, string>();
            // Validate every collision and missing password before provisioning any identity.
            foreach (var account in Accounts)
            {
                var existing = await db.Users.Include(u => u.Role).Include(u => u.Customer)
                    .Include(u => u.Technician).SingleOrDefaultAsync(u => u.Email.ToLower() == account.Email);
                if (existing != null)
                {
                    if (existing.Role.RoleName != account.Role || existing.FullName != account.Name ||
                        (account.Role == ServiceOpsRoles.Customer && existing.Customer == null) ||
                        (account.Role == ServiceOpsRoles.Technician && existing.Technician == null))
                        throw new InvalidOperationException($"Demo identity collision for {account.Email}; review it without replacing the existing account.");
                    continue;
                }
                var password = config[$"SeedDemo:Passwords:{account.Key}"] ?? config["SeedDemo:Password"];
                if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 1024)
                    throw new InvalidOperationException($"Configure a private 12–1024 character SeedDemo password for {account.Key} before enabling the seed.");
                passwords[account.Email] = password;
            }
            foreach (var account in Accounts.Where(a => passwords.ContainsKey(a.Email)))
            {
                var roleId = await db.Roles.Where(r => r.RoleName == account.Role).Select(r => r.RoleId).SingleAsync();
                // Existing provisioning hashes securely and saves Customer/Technician profiles atomically.
                await users.CreateUserAsync(new CreateUserRequest
                {
                    FullName = account.Name, Email = account.Email, Password = passwords[account.Email],
                    ConfirmPassword = passwords[account.Email], RoleId = roleId,
                    CustomerName = account.Role == ServiceOpsRoles.Customer ? "Sunrise Office Solutions (fictional demo)" : null,
                    TechnicianSkill = account.Role == ServiceOpsRoles.Technician ? "HVAC inspection and electrical maintenance" : null
                });
            }
            await SeedBusinessDataAsync(db);
        }
        finally
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({SeedLock})");
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task SeedBusinessDataAsync(AppDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var customer = await db.Customers.SingleAsync(c => c.User.Email == "customer.demo@serviceops.local");
        var technician = await db.Technicians.Include(t => t.User).SingleAsync(t => t.User.Email == "technician.demo@serviceops.local");
        var site = await db.CustomerSites.SingleOrDefaultAsync(s => s.CustomerId == customer.CustomerId && s.LocationDetails == Marker);
        if (site == null)
        {
            site = new CustomerSite { CustomerId = customer.CustomerId, SiteName = "Colombo Main Office (demo)",
                AddressLine = "Fictional office service location", City = "Colombo", Province = "Western", LocationDetails = Marker };
            db.CustomerSites.Add(site);
            await db.SaveChangesAsync();
        }
        var asset = await db.Assets.SingleOrDefaultAsync(a => a.SerialNumber == "SERVICEOPS-DEMO-AC-001");
        if (asset != null && asset.SiteId != site.SiteId)
            throw new InvalidOperationException("Demo asset serial collision; existing equipment was preserved.");
        if (asset == null)
        {
            asset = new Asset { SiteId = site.SiteId, AssetName = "Air Conditioning Unit AC-001", Category = "HVAC",
                SerialNumber = "SERVICEOPS-DEMO-AC-001", WarrantyInformation = "Fictional academic demonstration equipment" };
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
        }

        // Stable category keys survive status progression; reruns never reset a completed request.
        var assignedRequest = await RequestAsync(db, customer, site, asset, "DEMO-HVAC-ASSIGNED-v1",
            "Air conditioning system not cooling", Priority.High);
        await RequestAsync(db, customer, site, asset, "DEMO-HVAC-NEW-v1",
            "Unusual vibration during air conditioning startup", Priority.Medium);
        var job = await db.WorkOrders.SingleOrDefaultAsync(w => w.ServiceRequestId == assignedRequest.ServiceRequestId);
        if (job == null)
        {
            if (assignedRequest.Status != ServiceStatus.New || technician.User.AccountStatus != "Active" || !technician.Availability)
                throw new InvalidOperationException("Demo assignment requires a New request and active, available demo technician; no existing state was changed.");
            assignedRequest.Status = ServiceStatus.Assigned;
            assignedRequest.UpdatedAt = DateTime.UtcNow;
            job = new WorkOrder { ServiceRequestId = assignedRequest.ServiceRequestId, TechnicianId = technician.TechnicianId,
                Status = ServiceStatus.Assigned, JobNote = Marker + ": inspect cooling performance and electrical connections." };
            job.JobStatusHistories.Add(new JobStatusHistory { Status = ServiceStatus.Assigned, Remarks = Marker + ": assigned for the academic walkthrough." });
            foreach (var item in new[] { "Isolate electrical supply and verify safe access", "Inspect air filter and cooling performance", "Check capacitor and electrical connections" })
                job.ChecklistItems.Add(new ServiceChecklistItem { Description = item });
            db.WorkOrders.Add(job);
            technician.CurrentWorkload = await db.WorkOrders.CountAsync(w => w.TechnicianId == technician.TechnicianId &&
                w.Status != ServiceStatus.Completed && w.Status != ServiceStatus.Closed) + 1;
        }
        // No fabricated completion, part issue or maintenance: these require the live domain workflow.
        await PartAsync(db, "Demo HVAC run capacitor", 12, 3);
        await PartAsync(db, "Demo HVAC replacement filter", 2, 3);
        foreach (var account in Accounts)
        {
            var userId = await db.Users.Where(u => u.Email == account.Email).Select(u => u.UserId).SingleAsync();
            var message = Marker + ": Sunrise Office Solutions sample records are ready for the service walkthrough.";
            if (!await db.Notifications.AnyAsync(n => n.UserId == userId && n.Message == message))
                db.Notifications.Add(new Notification { UserId = userId, Message = message });
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static async Task<ServiceRequest> RequestAsync(AppDbContext db, Customer customer, CustomerSite site,
        Asset asset, string category, string issue, Priority priority)
    {
        var request = await db.ServiceRequests.SingleOrDefaultAsync(r => r.CustomerId == customer.CustomerId && r.Category == category);
        if (request == null)
        {
            request = new ServiceRequest { CustomerId = customer.CustomerId, SiteId = site.SiteId, AssetId = asset.AssetId,
                Category = category, IssueDescription = issue, Priority = priority, Status = ServiceStatus.New };
            db.ServiceRequests.Add(request);
            await db.SaveChangesAsync();
        }
        else if (request.SiteId != site.SiteId || request.AssetId != asset.AssetId)
            throw new InvalidOperationException("Demo request ownership changed; review its associations without resetting data.");
        return request;
    }

    private static async Task PartAsync(AppDbContext db, string name, int quantity, int threshold)
    {
        var existing = await db.SpareParts.SingleOrDefaultAsync(p => p.PartName == name);
        if (existing != null)
        {
            if (existing.Description != Marker) throw new InvalidOperationException("Demo part name collision; existing inventory was preserved.");
            return;
        }
        var part = new SparePart { PartName = name, Description = Marker, QuantityInStock = quantity, LowStockLevel = threshold };
        part.InventoryTransactions.Add(new InventoryTransaction { TransactionType = InventoryTransactionType.StockAddition,
            Quantity = quantity, Remarks = Marker + ": initial fictional stock" });
        db.SpareParts.Add(part);
    }
}
