export type RecordRoute = { resource: string; id: number; edit: boolean };
export const recordResources: Record<string, { api: string; key: string; label: string }> = {
  customers: { api: "customers", key: "customerId", label: "Customer" },
  sites: { api: "sites", key: "siteId", label: "Service location" },
  assets: { api: "assets", key: "assetId", label: "Equipment" },
  requests: { api: "requests", key: "serviceRequestId", label: "Service request" },
  "work-orders": { api: "work-orders", key: "workOrderId", label: "Work order" },
  inventory: { api: "inventory/parts", key: "sparePartId", label: "Spare part" },
  users: { api: "users", key: "userId", label: "User" },
  audit: { api: "administration/audit", key: "auditLogId", label: "Audit entry" },
  inquiries: { api: "contact-inquiries", key: "contactInquiryId", label: "Contact inquiry" },
};
export function recordHref(page: string, row: Record<string, unknown>, edit = false) {
  const resource = page === "inventory/parts" ? "inventory" : page;
  const spec = recordResources[resource];
  return spec ? `/dashboard/${resource}/${row[spec.key]}${edit ? "/edit" : ""}` : null;
}
