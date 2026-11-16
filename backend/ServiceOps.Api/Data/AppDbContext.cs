using Microsoft.EntityFrameworkCore;
using ServiceOps.Api.Models;

namespace ServiceOps.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Keep the business write and generated-key audit repair in one transaction.
    // Respect transactions already owned by workflow services/controllers.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (Database.CurrentTransaction != null)
            return base.SaveChanges(acceptAllChangesOnSuccess);
        using var transaction = Database.BeginTransaction();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        transaction.Commit();
        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction != null)
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerSite> CustomerSites => Set<CustomerSite>();
    public DbSet<Asset> Assets => Set<Asset>();

    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<JobStatusHistory> JobStatusHistories => Set<JobStatusHistory>();
    public DbSet<ServiceChecklistItem> ServiceChecklistItems => Set<ServiceChecklistItem>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<SparePart> SpareParts => Set<SparePart>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<CustomerConfirmation> CustomerConfirmations => Set<CustomerConfirmation>();
    public DbSet<CustomerFeedback> CustomerFeedbacks => Set<CustomerFeedback>();
    public DbSet<ServiceReport> ServiceReports => Set<ServiceReport>();

    public DbSet<ContactInquiry> ContactInquiries => Set<ContactInquiry>();

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<CustomerSite>().Property(s => s.MapsUrl).HasMaxLength(2048);
        modelBuilder.Entity<ContactInquiry>(inquiry =>
        {
            inquiry.HasKey(i => i.ContactInquiryId);
            inquiry.HasIndex(i => i.SubmissionId).IsUnique();
            inquiry.HasIndex(i => i.CreatedAt);
            inquiry.Property(i => i.FullName).HasMaxLength(100).IsRequired();
            inquiry.Property(i => i.Email).HasMaxLength(254).IsRequired();
            inquiry.Property(i => i.PhoneNumber).HasMaxLength(32);
            inquiry.Property(i => i.Subject).HasMaxLength(200).IsRequired();
            inquiry.Property(i => i.Message).HasMaxLength(4000).IsRequired();
            inquiry.HasOne(i => i.ReviewedByUser).WithMany().HasForeignKey(i => i.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RefreshSession>(session=>
        {
            session.HasKey(s=>s.RefreshSessionId);
            session.Property(s=>s.TokenHash).HasMaxLength(64).IsRequired();
            session.Property(s=>s.PasswordVersion).HasMaxLength(64).IsRequired();
            session.HasIndex(s=>s.TokenHash).IsUnique();
            session.HasIndex(s=>s.FamilyId);
            session.HasIndex(s=>new {s.UserId,s.ExpiresAt});
            session.HasOne(s=>s.User).WithMany().HasForeignKey(s=>s.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>()
        .HasKey(r => r.RoleId);

    modelBuilder.Entity<Permission>()
        .HasKey(p => p.PermissionId);

    modelBuilder.Entity<User>()
        .HasKey(u => u.UserId);

    modelBuilder.Entity<Customer>()
        .HasKey(c => c.CustomerId);

    modelBuilder.Entity<Customer>()
        .Property(c => c.Notes).HasMaxLength(2000);

    modelBuilder.Entity<CustomerSite>()
        .HasKey(cs => cs.SiteId);

    modelBuilder.Entity<Asset>()
        .HasKey(a => a.AssetId);

    modelBuilder.Entity<ServiceRequest>()
        .HasKey(sr => sr.ServiceRequestId);

    modelBuilder.Entity<Technician>()
        .HasKey(t => t.TechnicianId);

    modelBuilder.Entity<WorkOrder>()
        .HasKey(wo => wo.WorkOrderId);

    modelBuilder.Entity<JobStatusHistory>()
        .HasKey(j => j.StatusHistoryId);

    modelBuilder.Entity<ServiceChecklistItem>()
        .HasKey(sc => sc.ChecklistItemId);

    modelBuilder.Entity<Attachment>()
        .HasKey(a => a.AttachmentId);

    modelBuilder.Entity<SparePart>()
        .HasKey(sp => sp.SparePartId);

    modelBuilder.Entity<InventoryTransaction>()
        .HasKey(it => it.TransactionId);

    modelBuilder.Entity<MaintenanceRecord>()
        .HasKey(m => m.MaintenanceRecordId);

    modelBuilder.Entity<CustomerFeedback>()
        .HasKey(cf => cf.FeedbackId);

    modelBuilder.Entity<ServiceReport>()
        .HasKey(sr => sr.ServiceReportId);

    modelBuilder.Entity<Notification>()
        .HasKey(n => n.NotificationId);

    modelBuilder.Entity<AuditLog>()
        .HasKey(a => a.AuditLogId);

        // RolePermission composite primary key
        modelBuilder.Entity<RolePermission>()
            .HasKey(rp => new { rp.RoleId, rp.PermissionId });

        modelBuilder.Entity<CustomerConfirmation>()
            .HasKey(cc => cc.ConfirmationId);

        // Role -> Users
        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Role -> Permission many-to-many through RolePermission
        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId);

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId);

        // User -> Customer
        modelBuilder.Entity<Customer>()
            .HasOne(c => c.User)
            .WithOne(u => u.Customer)
            .HasForeignKey<Customer>(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // User -> Technician
        modelBuilder.Entity<Technician>()
            .HasOne(t => t.User)
            .WithOne(u => u.Technician)
            .HasForeignKey<Technician>(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Customer -> Customer Sites
        modelBuilder.Entity<CustomerSite>()
            .HasOne(cs => cs.Customer)
            .WithMany(c => c.CustomerSites)
            .HasForeignKey(cs => cs.CustomerId);

        // Customer Site -> Assets
        modelBuilder.Entity<Asset>()
            .HasOne(a => a.CustomerSite)
            .WithMany(cs => cs.Assets)
            .HasForeignKey(a => a.SiteId);

        // Customer -> Service Requests
        modelBuilder.Entity<ServiceRequest>()
            .HasOne(sr => sr.Customer)
            .WithMany(c => c.ServiceRequests)
            .HasForeignKey(sr => sr.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Customer Site -> Service Requests
        modelBuilder.Entity<ServiceRequest>()
            .HasOne(sr => sr.CustomerSite)
            .WithMany(cs => cs.ServiceRequests)
            .HasForeignKey(sr => sr.SiteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Asset -> Service Requests
        modelBuilder.Entity<ServiceRequest>()
            .HasOne(sr => sr.Asset)
            .WithMany(a => a.ServiceRequests)
            .HasForeignKey(sr => sr.AssetId)
            .OnDelete(DeleteBehavior.SetNull);

        // Service Request -> Work Order one-to-one
        modelBuilder.Entity<WorkOrder>()
            .HasOne(wo => wo.ServiceRequest)
            .WithOne(sr => sr.WorkOrder)
            .HasForeignKey<WorkOrder>(wo => wo.ServiceRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        // Technician -> Work Orders
        modelBuilder.Entity<WorkOrder>()
            .HasOne(wo => wo.Technician)
            .WithMany(t => t.WorkOrders)
            .HasForeignKey(wo => wo.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict);

        // Work Order -> Status History
        modelBuilder.Entity<JobStatusHistory>()
            .HasOne(j => j.WorkOrder)
            .WithMany(wo => wo.JobStatusHistories)
            .HasForeignKey(j => j.WorkOrderId);

        // Work Order -> Checklist
        modelBuilder.Entity<ServiceChecklistItem>()
            .HasOne(c => c.WorkOrder)
            .WithMany(wo => wo.ChecklistItems)
            .HasForeignKey(c => c.WorkOrderId);

        // Service Request -> Attachments
        modelBuilder.Entity<Attachment>()
            .HasOne(a => a.ServiceRequest)
            .WithMany(sr => sr.Attachments)
            .HasForeignKey(a => a.ServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Work Order -> Attachments
        modelBuilder.Entity<Attachment>()
            .HasOne(a => a.WorkOrder)
            .WithMany(wo => wo.Attachments)
            .HasForeignKey(a => a.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Spare Part -> Inventory Transactions
        modelBuilder.Entity<InventoryTransaction>()
            .HasOne(it => it.SparePart)
            .WithMany(sp => sp.InventoryTransactions)
            .HasForeignKey(it => it.SparePartId)
            .OnDelete(DeleteBehavior.Restrict);

        // Work Order -> Inventory Transactions
        modelBuilder.Entity<InventoryTransaction>()
            .HasOne(it => it.WorkOrder)
            .WithMany(wo => wo.InventoryTransactions)
            .HasForeignKey(it => it.WorkOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        // Asset -> Maintenance Records
        modelBuilder.Entity<MaintenanceRecord>()
            .HasOne(m => m.Asset)
            .WithMany(a => a.MaintenanceRecords)
            .HasForeignKey(m => m.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        // Work Order -> Maintenance Records
        modelBuilder.Entity<MaintenanceRecord>()
            .HasOne(m => m.WorkOrder)
            .WithMany(wo => wo.MaintenanceRecords)
            .HasForeignKey(m => m.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Work Order -> Customer Confirmation one-to-one
        modelBuilder.Entity<CustomerConfirmation>()
            .HasOne(cc => cc.WorkOrder)
            .WithOne(wo => wo.CustomerConfirmation)
            .HasForeignKey<CustomerConfirmation>(cc => cc.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Customer -> Confirmations
        modelBuilder.Entity<CustomerConfirmation>()
            .HasOne(cc => cc.Customer)
            .WithMany(c => c.Confirmations)
            .HasForeignKey(cc => cc.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Customer -> Feedback
        modelBuilder.Entity<CustomerFeedback>()
            .HasOne(cf => cf.Customer)
            .WithMany(c => c.Feedbacks)
            .HasForeignKey(cf => cf.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Feedback -> Work Order
        modelBuilder.Entity<CustomerFeedback>()
            .HasOne(cf => cf.WorkOrder)
            .WithOne()
            .HasForeignKey<CustomerFeedback>(cf => cf.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Work Order -> Service Report one-to-one
        modelBuilder.Entity<ServiceReport>()
            .HasOne(sr => sr.WorkOrder)
            .WithOne(wo => wo.ServiceReport)
            .HasForeignKey<ServiceReport>(sr => sr.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // User -> Notifications
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId);

        // User -> Audit Logs
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CustomerSite>().ToTable(t => t.HasCheckConstraint("CK_CustomerSites_Coordinates", "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) OR (\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL AND \"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180)"));
        modelBuilder.Entity<CustomerSite>().Property(s => s.Latitude).HasPrecision(9, 6);
        modelBuilder.Entity<CustomerSite>().Property(s => s.Longitude).HasPrecision(9, 6);
        modelBuilder.Entity<User>().Property(u => u.MustChangePassword).HasDefaultValue(false);

        // Unique values
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Role>()
            .HasIndex(r => r.RoleName)
            .IsUnique();

        modelBuilder.Entity<Permission>()
            .HasIndex(p => p.PermissionName)
            .IsUnique();

        // Feedback rating 1-5
        modelBuilder.Entity<CustomerFeedback>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_CustomerFeedback_Rating",
                "\"Rating\" >= 1 AND \"Rating\" <= 5"));

        // Inventory values cannot be negative
        modelBuilder.Entity<SparePart>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_SparePart_QuantityInStock",
                "\"QuantityInStock\" >= 0"));
    }
}