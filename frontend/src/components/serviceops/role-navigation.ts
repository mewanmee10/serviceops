import {
  Activity,
  Bell,
  Boxes,
  Building2,
  ClipboardList,
  History,
  LayoutDashboard,
  ShieldCheck,
  Users,
  Wrench,
  type LucideIcon,
} from "lucide-react";

export const serviceOpsRoles = [
  "Administrator",
  "Service Manager",
  "Technician",
  "Inventory Staff",
  "Customer",
] as const;
export type ServiceOpsRole = (typeof serviceOpsRoles)[number];
export type NavigationItem = {
  id: string;
  page: string;
  label: string;
  icon: LucideIcon;
};
const item = (
  id: string,
  label: string,
  icon: LucideIcon,
  page = id,
): NavigationItem => ({ id, page, label, icon });
const security = item("security", "Profile & Security", ShieldCheck);
const notifications = item("notifications", "Notifications", Bell);

/** Presentation only. APIs enforce roles, capability grants and record ownership. */
export const roleNavigation: Record<ServiceOpsRole, NavigationItem[]> = {
  Administrator: [
    item("dashboard", "Overview", LayoutDashboard),
    item("users", "User Management", Users),
    item("administration", "Roles & Permissions", ShieldCheck),
    item("customers", "Customers", Users),
    item("sites", "Customer Sites", Building2),
    item("assets", "Equipment", Boxes),
    item("analytics", "Reports & Analytics", Activity),
    item("audit", "Audit Logs", History, "administration"),
    item("inquiries", "Contact Inquiries", Bell),
    item("monitoring", "System Monitoring", Activity),
    security,
    item("requests", "Service Requests", ClipboardList),
    item("work-orders", "Work Orders", Wrench),
    item("maintenance", "Maintenance History", History),
    item("technicians", "Technicians", Wrench),
    item("inventory/parts", "Spare Parts", Boxes),
    item("inventory-summary", "Inventory Reports", Boxes),
    notifications,
  ],
  "Service Manager": [
    item("dashboard", "Overview", LayoutDashboard),
    item("customers", "Customers", Users),
    item("sites", "Customer Sites", Building2),
    item("assets", "Equipment", Boxes),
    item("requests", "Service Requests", ClipboardList),
    item("technicians", "Technicians", Users),
    item(
      "scheduling",
      "Technician Assignment / Scheduling",
      Wrench,
      "requests",
    ),
    item("work-orders", "Work Orders", Wrench),
    item("monitor-jobs", "Job Monitoring", Activity, "work-orders"),
    item("maintenance", "Maintenance History", History),
    item("analytics", "Reports & Analytics", Activity),
    notifications,
    security,
  ],
  Technician: [
    item("dashboard", "My Dashboard", LayoutDashboard),
    item("work-orders", "My Assigned Jobs", Wrench),
    item("my-work-orders", "My Work Orders", ClipboardList, "work-orders"),
    item("job-progress", "Job Progress & Checklists", Activity, "work-orders"),
    item("maintenance", "Equipment & Service History", History),
    item("parts-used", "Parts Used", Boxes, "work-orders"),
    item("notifications", "My Notifications", Bell),
    security,
  ],
  "Inventory Staff": [
    item("dashboard", "Inventory Overview", LayoutDashboard),
    item("inventory/parts", "Spare Parts", Boxes),
    item("inventory/transactions", "Stock Movements", History),
    item("parts-issues", "Parts Issues", Boxes, "inventory/parts"),
    item("parts-consumption", "Parts Consumption", Boxes, "inventory/parts"),
    item(
      "inventory-history",
      "Inventory History",
      History,
      "inventory/transactions",
    ),
    item("inventory/work-orders", "Work Order References", Wrench),
    item("low-stock", "Low Stock", Boxes, "inventory/parts"),
    item("inventory-summary", "Inventory Reports", Activity),
    notifications,
    security,
  ],
  Customer: [
    item("dashboard", "My Dashboard", LayoutDashboard),
    item("customers", "My Profile", Users),
    item("requests", "My Service Requests", ClipboardList),
    item("create-request", "Create Service Request", ClipboardList, "requests"),
    item("request-tracking", "Request Tracking", Activity, "requests"),
    item("sites", "My Service Locations", Building2),
    item("assets", "My Equipment", Boxes),
    item("maintenance", "My Service History", History),
    item("work-orders", "Completed Work", Wrench),
    item("service-reports", "Service Reports", History, "work-orders"),
    item("notifications", "My Notifications", Bell),
    security,
  ],
};

export function navigationFor(
  role: string | undefined,
  permissions: string[] = [],
): NavigationItem[] {
  const required: Record<string, string> = {
    users: "users.manage",
    administration: "permissions.manage",
    audit: "audit.read",
    monitoring: "audit.read",
    analytics: "analytics.read",
    "service-reports": "reports.read",
  };
  const entries = serviceOpsRoles.includes(role as ServiceOpsRole)
    ? [...roleNavigation[role as ServiceOpsRole]]
    : [];
  return serviceOpsRoles.includes(role as ServiceOpsRole)
    ? entries.filter((n) => {
        const permission =
          n.page.startsWith("inventory") || n.id === "low-stock"
            ? "inventory.manage"
            : required[n.id];
        return !permission || permissions.includes(permission);
      })
    : [];
}
