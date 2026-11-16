import type { Row } from "@/lib/serviceops-api";
import type { Field } from "./record-form";
export type RecordFormSpec = { fields: Field[]; path: string; method?: string; initial?: Row };
export async function editFields(page: string, role: string | undefined, row: Row, options: (path: string, id: string, label: string) => Promise<{ value: string; label: string }[]>): Promise<RecordFormSpec | null> {

      if (page === "users")
        return ({
          path: `users/${row.userId}`,
          method: "PUT",
          initial: row,
          fields: [
            { key: "fullName", label: "Full name", required: true },
            { key: "email", label: "Email", type: "email", required: true },
            { key: "phoneNumber", label: "Phone" },
            {
              key: "roleId",
              label: "Role",
              type: "number",
              required: true,
              options: await options("users/roles", "roleId", "roleName"),
            },
          ],
        });
      if (page === "customers")
        return ({
          path: `customers/${row.customerId}`,
          method: "PUT",
          initial: { ...row, name: row.customerName },
          fields: [
            ...(role === "Customer"
              ? []
              : [
                  {
                    key: "userId",
                    label: "Linked account ID",
                    type: "number",
                    required: true,
                  },
                ]),
            {
              key: "name",
              label: "Customer name",
              required: true,
              maxLength: 200,
            },
            {
              key: "phoneNumber",
              label: "Contact phone",
              type: "tel",
              maxLength: 30,
            },
            { key: "addressLine", label: "Address" },
            { key: "city", label: "City" },
            { key: "province", label: "Province" },
            { key: "postalCode", label: "Postal code" },
            {
              key: "notes",
              label: "Customer notes (visible to customer and staff)",
              type: "textarea",
              maxLength: 2000,
            },
          ],
        });
      if (page === "sites")
        return ({
          path: `sites/${row.siteId}`,
          method: "PUT",
          initial: row,
          fields: [
            {
              key: "customerId",
              label: "Customer ID",
              type: "number",
              required: true,
            },
            { key: "siteName", label: "Site name", required: true },
            { key: "addressLine", label: "Address", required: true },
            { key: "city", label: "City" },
            { key: "province", label: "Province" },
            { key: "postalCode", label: "Postal code" },
            { key: "locationDetails", label: "Location details" },
            { key: "mapsUrl", label: "Google Maps Location Link", type: "url", maxLength: 2048, placeholder: "Paste a Google Maps location-sharing link", help: "Open Google Maps, select the service location, choose Share, and paste the location link here." },
          ],
        });
      if (page === "assets")
        return ({
          path: `assets/${row.assetId}`,
          method: "PUT",
          initial: row,
          fields: [
            { key: "siteId", label: "Site ID", type: "number", required: true },
            { key: "assetName", label: "Equipment name", required: true },
            { key: "category", label: "Category" },
            { key: "serialNumber", label: "Serial number" },
            {
              key: "installationDate",
              label: "Installation date",
              type: "date",
            },
            { key: "warrantyInformation", label: "Warranty information" },
          ],
        });
      if (page === "technicians")
        return ({
          path: `technicians/${row.technicianId}`,
          method: "PUT",
          initial: row,
          fields: [
            {
              key: "userId",
              label: "Account ID",
              type: "number",
              required: true,
            },
            { key: "skill", label: "Skills", required: true },
            {
              key: "availability",
              label: "Available for assignment",
              type: "boolean",
              required: true,
              options: [
                { value: "true", label: "Available" },
                { value: "false", label: "Unavailable" },
              ],
            },
          ],
        });
      if (page === "inventory/parts")
        return ({
          path: `inventory/parts/${row.sparePartId}`,
          method: "PUT",
          initial: row,
          fields: [
            { key: "partName", label: "Part name", required: true },
            { key: "description", label: "Description" },
            {
              key: "lowStockLevel",
              label: "Reorder threshold",
              type: "number",
              required: true,
            },
          ],
        });
    if (page === "requests") return { path: `requests/${row.serviceRequestId}`, method: "PUT", initial: row, fields: [
      { key: "category", label: "Category", required: true, maxLength: 100 },
      { key: "issueDescription", label: "Describe the issue", type: "textarea", required: true, maxLength: 4000 },
    ] };
    return null;
}
