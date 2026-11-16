"use client";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, ApiError, type Row } from "@/lib/serviceops-api";
import { directionsUrl } from "@/lib/maps-link";
import { recordResources, type RecordRoute } from "@/lib/record-routes";
import { editFields, type RecordFormSpec } from "./record-fields";
import { Form } from "./record-form";
import { WorkOrderDetail } from "./work-order-detail";
import { Attachments } from "./attachments";

const labels = (key: string) => key.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/^./, c => c.toUpperCase());
export function DedicatedRecord({ route, role, userId, permissions }: { route: RecordRoute; role: string; userId: number; permissions: string[] }) {
  const spec = recordResources[route.resource], endpoint = `${spec.api}/${route.id}`;
  const [record, setRecord] = useState<Record<string, unknown> | null>(null);
  const [form, setForm] = useState<RecordFormSpec | null>(null);
  const [error, setError] = useState(""), [status, setStatus] = useState(0), [message, setMessage] = useState("");
  const backSection = route.resource === "inventory" ? "inventory/parts" : route.resource === "audit" ? "audit" : route.resource;
  const back = `/dashboard?section=${encodeURIComponent(backSection)}`;
  const reload = useCallback(async () => { setRecord(await api(endpoint)); }, [endpoint]);
  const options = useCallback(async (path: string, id: string, label: string) => {
    const rows: Row[] = await api(path); return rows.map(r => ({ value: String(r[id]), label: String(r[label]) }));
  }, []);
  useEffect(() => {
    let active = true;
    api(endpoint).then(async value => {
      const edit = route.edit ? await editFields(spec.api, role, value, options) : null;
      if (active) { setRecord(value); setForm(edit); if (route.edit && !edit) { setStatus(405); setError("This record does not support editing here."); } }
    }).catch(e => { if (active) { setError(e.message); setStatus(e instanceof ApiError ? e.status : 500); } });
    return () => { active = false; };
  }, [endpoint, route.edit, spec.api, role, options]);
  async function action(path: string, method: string, body?: object) {
    try { await api(path, method, body); await reload(); setMessage("Changes saved."); setError(""); }
    catch (e) { setError((e as Error).message); }
  }
  async function assignment() {
    setForm({ path: `${endpoint}/assignment`, method: "PATCH", initial: record as Row, fields: [
      { key: "technicianId", label: "Technician", type: "number", required: true, options: await options("technicians", "technicianId", "fullName") },
      { key: "scheduledVisit", label: "Scheduled visit", type: "datetime-local" }, { key: "jobNote", label: "Job note" },
    ] });
  }
  const editable = ["customers", "sites", "assets", "users", "inventory"].includes(route.resource) || (route.resource === "requests" && record?.status === 0 && ["Administrator", "Service Manager"].includes(role));
  return <>
    <nav aria-label="Breadcrumb" className="record-breadcrumb"><Link href={back}>← Back to {spec.label.toLowerCase()} list</Link><span> / {spec.label} #{route.id}{route.edit ? " / Edit" : ""}</span></nav>
    {error && <section className="panel"><h2>{status === 404 ? "Record not found" : status === 403 ? "Access denied" : "Unable to complete this action"}</h2><p role="alert" className="error">{error}</p></section>}
    {message && <p role="status" className="success">{message}</p>}
    {!record && !error && <p role="status">Loading record…</p>}
    {record && form && <section className="panel"><h2>{route.edit ? `Edit ${spec.label.toLowerCase()}` : "Update assignment"}</h2><Form fields={form.fields} initial={form.initial} onSubmit={async value => {
      await api(form.path, form.method || "PUT", { ...value,
        ...(["sites", "customers", "assets"].includes(route.resource) ? { replaceDetails: true } : {}),
        ...(route.resource === "customers" ? { userId: record.userId ?? userId } : {}),
      });
      await reload(); setMessage("Record saved.");
      if (!route.edit) setForm(null);
    }}/><Link href={route.edit ? `/dashboard/${route.resource}/${route.id}` : back}>Cancel</Link></section>}
    {record && !route.edit && route.resource === "work-orders" ? <WorkOrderDetail detail={record} role={role} permissions={permissions} reload={reload} action={action} editAssignment={() => void assignment()} onClose={() => window.history.back()} onSuccess={setMessage} /> : record && !route.edit ? <section className="panel">
      <div className="panel-title"><h2>{spec.label} #{route.id}</h2>{editable && <Link className="button-link" href={`/dashboard/${route.resource}/${route.id}/edit`}>Edit</Link>}</div>
      <dl className="record-properties">{Object.entries(record).filter(([key, value]) => !["latitude", "longitude", "mapsUrl"].includes(key) && (value === null || typeof value !== "object")).map(([key, value]) => <div key={key}><dt>{labels(key)}</dt><dd>{value === null || value === "" ? "Not recorded" : typeof value === "boolean" ? value ? "Yes" : "No" : /At$|Date$|Time$|Visit$/.test(key) ? new Date(String(value)).toLocaleString() : String(value)}</dd></div>)}</dl>
      {route.resource === "sites" && directionsUrl(record) && <a className="button-link" href={directionsUrl(record)!} target="_blank" rel="noopener noreferrer">Open in Google Maps ↗</a>}
      {route.resource === "requests" && <Attachments resource="requests" id={route.id} />}
      {route.resource === "inquiries" && !record.reviewedAt && <button className="primary" onClick={() => void action(`${endpoint}/review`, "PATCH")}>Mark reviewed</button>}
    </section> : null}
  </>;
}
