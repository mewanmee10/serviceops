"use client";
import { api, type Row } from "@/lib/serviceops-api";
import { Form } from "./record-form";
import { Attachments } from "./attachments";
import { PartsHandoff } from "./parts-handoff";
import { directionsUrl } from "@/lib/maps-link";
const statuses=["New","Assigned","In progress","On hold","Completed","Closed"];
export function WorkOrderDetail({detail, role, permissions, reload, action, editAssignment, onClose, onSuccess}: {
 detail: Record<string, unknown>; role: string; permissions: string[]; reload:()=>Promise<void>;
 action:(path:string,method:string,body?:object)=>Promise<void>; editAssignment:()=>void; onClose:()=>void; onSuccess:(value:string)=>void;
}) {
 const staff=role==="Administrator"||role==="Service Manager";
 const mapsHref=directionsUrl(detail);
 return (
              <section className="panel">
                <div className="panel-title">
                  <h2>Work order #{String(detail.workOrderId)}</h2>
                  {staff && detail.status === 1 && (
                    <button onClick={editAssignment}>
                      Edit assignment / schedule
                    </button>
                  )}
                  <button onClick={() => onClose()}>Close</button>
                </div>
                <p>
                  <span className="badge">
                    {statuses[Number(detail.status)]}
                  </span>{" "}
                  · Request #{String(detail.serviceRequestId)}
                </p>
                <p className="muted">
                  {String(detail.customerName)} · {String(detail.siteName)} ·{" "}
                  {String(detail.addressLine)}
                  <br />
                  Contact: {String(
                    detail.customerPhone || "Not recorded",
                  )} · {String(detail.customerEmail || "Not recorded")}
                  <br />
                  {String(detail.assetName || "No equipment linked")} ·{" "}
                  {String(detail.issueDescription)}
                </p>
                {Boolean(detail.assetName) && (
                  <p className="muted">
                    Serial: {String(detail.serialNumber || "Not recorded")} ·
                    Installed:{" "}
                    {detail.installationDate
                      ? new Date(
                          String(detail.installationDate),
                        ).toLocaleDateString()
                      : "Not recorded"}
                    <br />
                    Warranty:{" "}
                    {String(detail.warrantyInformation || "Not recorded")}
                  </p>
                )}
                {role === "Technician" && (mapsHref ? <a className="primary external-map-link" href={mapsHref} target="_blank" rel="noopener noreferrer">Open in Google Maps ↗</a> : <p className="muted">No usable service location address or Maps link is recorded.</p>)}
                <div className="job-notes">
                  <p>
                    <strong>Job note</strong>
                    <br />
                    {String(detail.jobNote || "No notes")}
                  </p>
                  <p>
                    <strong>Diagnosis</strong>
                    <br />
                    {String(detail.diagnosis || "Not recorded")}
                  </p>
                  <p>
                    <strong>Work performed</strong>
                    <br />
                    {String(detail.workPerformed || "Not recorded")}
                  </p>
                </div>
                <h3>Spare-parts handoff</h3>
                <p className="muted">
                  Inventory staff record issue, consumption and returns against
                  this work order. Coordinate actual quantities with them before
                  completing the job.
                </p>
                {((detail.parts as Row[]) || []).map((part) => (
                  <p key={String(part.transactionId)}>
                    {String(part.partName)} ·{" "}
                    {
                      [
                        "Addition / return",
                        "Reduction",
                        "Issue",
                        "Consumption",
                        "Adjustment",
                      ][Number(part.transactionType)]
                    }{" "}
                    · {String(part.quantity)} units
                  </p>
                ))}
                {((detail.parts as Row[]) || []).length === 0 && (
                  <p>No parts recorded.</p>
                )}
                {role === "Technician" && detail.status === 2 && (
                  <PartsHandoff
                    workOrderId={Number(detail.workOrderId)}
                    onSaved={async () =>
                      await reload()
                    }
                  />
                )}
                <h3>Service checklist</h3>
                {(detail.checklist as Row[]).map((c) => (
                  <label className="check" key={String(c.checklistItemId)}>
                    <input
                      type="checkbox"
                      checked={Boolean(c.isCompleted)}
                      disabled={role !== "Technician" || detail.status !== 2}
                      onChange={(e) =>
                        action(
                          `work-orders/${detail.workOrderId}/checklist/${c.checklistItemId}`,
                          "PATCH",
                          { isCompleted: e.target.checked },
                        )
                      }
                    />
                    {String(c.description)}
                  </label>
                ))}
                {staff && Number(detail.status) < 4 && (
                  <Form
                    fields={[
                      {
                        key: "description",
                        label: "Checklist description",
                        required: true,
                      },
                    ]}
                    submit="Add checklist item"
                    onSubmit={async (v) => {
                      await api(
                        `work-orders/${detail.workOrderId}/checklist`,
                        "POST",
                        v,
                      );
                      await reload();
                    }}
                  />
                )}
                {role === "Technician" && Number(detail.status) < 4 && (
                  <Form
                    initial={{
                      jobNote: detail.jobNote as string,
                      diagnosis: detail.diagnosis as string,
                      workPerformed: detail.workPerformed as string,
                    }}
                    fields={[
                      {
                        key: "status",
                        label: "Next status",
                        type: "number",
                        required: true,
                        options: (detail.status === 1 || detail.status === 3
                          ? [2]
                          : [2, 3, 4]
                        ).map((i) => ({
                          value: String(i),
                          label: statuses[i],
                        })),
                      },
                      { key: "jobNote", label: "Job note", type: "textarea" },
                      {
                        key: "diagnosis",
                        label: "Diagnosis",
                        type: "textarea",
                      },
                      {
                        key: "workPerformed",
                        label: "Work performed",
                        type: "textarea",
                      },
                    ]}
                    submit="Update job"
                    onSubmit={async (v) => {
                      await api(
                        `work-orders/${detail.workOrderId}`,
                        "PATCH",
                        v,
                      );
                      await reload();
                      await reload();
                    }}
                  />
                )}
                {role === "Customer" && detail.status === 4 && (
                  <button
                    className="primary"
                    onClick={() => {
                      if (
                        confirm("Confirm that this service work is complete?")
                      )
                        void action(
                          `work-orders/${detail.workOrderId}/confirm`,
                          "POST",
                        );
                    }}
                  >
                    Confirm completion
                  </button>
                )}
                {role === "Customer" && detail.status === 5 && (
                  <Form
                    fields={[
                      {
                        key: "rating",
                        label: "Rating",
                        type: "number",
                        required: true,
                        options: [1, 2, 3, 4, 5].map((i) => ({
                          value: String(i),
                          label: `${i} / 5`,
                        })),
                      },
                      { key: "comments", label: "Feedback", type: "textarea" },
                    ]}
                    submit="Submit feedback"
                    onSubmit={async (v) => {
                      await api(
                        `work-orders/${detail.workOrderId}/feedback`,
                        "POST",
                        v,
                      );
                      onSuccess("Feedback submitted.");
                    }}
                  />
                )}
                {Number(detail.status) >= 4 &&
                  permissions.includes("reports.read") && (
                    <a
                      className="download"
                      style={{ marginRight: 20 }}
                      href={`/api/backend/reports/work-orders/${detail.workOrderId}/pdf`}
                    >
                      Download PDF report
                    </a>
                  )}
                {Number(detail.status) >= 4 &&
                  permissions.includes("reports.read") && (
                    <a
                      className="download"
                      href={`/api/backend/reports/work-orders/${detail.workOrderId}`}
                    >
                      Download service report (JSON)
                    </a>
                  )}
                <Attachments
                  resource="work-orders"
                  id={Number(detail.workOrderId)}
                />
                <h3>Status history</h3>
                <ol className="history">
                  {(detail.history as Row[]).map((h, i) => (
                    <li key={i}>
                      {statuses[Number(h.status)]} ·{" "}
                      {new Date(String(h.changedAt)).toLocaleString()}
                      <small>{String(h.remarks)}</small>
                    </li>
                  ))}
                </ol>
              </section>
);
}
