namespace ServiceOps.Api.Authorization;

public static class ServiceOpsRoles
{
    public static readonly string[] All = [Administrator, ServiceManager, Technician, InventoryStaff, Customer];

    public const string Administrator = "Administrator";
    public const string ServiceManager = "Service Manager";
    public const string Technician = "Technician";
    public const string InventoryStaff = "Inventory Staff";
    public const string Customer = "Customer";
}