"use client";
import { useEffect, useState } from "react";
import { api } from "@/lib/serviceops-api";
import { Form } from "./record-form";

export function PartsHandoff({
  workOrderId,
  onSaved,
}: {
  workOrderId: number;
  onSaved: () => Promise<void>;
}) {
  const [parts, setParts] = useState<
    { sparePartId: number; partName: string }[]
  >([]);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  useEffect(() => {
    let active = true;
    api(`work-orders/${workOrderId}/parts-catalog`)
      .then((data) => {
        if (active) setParts(data);
      })
      .catch((e) => {
        if (active) setError(e.message);
      });
    return () => {
      active = false;
    };
  }, [workOrderId]);
  return (
    <section aria-label="Record parts usage">
      <h3>Record usage for Inventory Staff</h3>
      <p className="muted">
        Declare actual parts used. This records job evidence and notifies
        Inventory Staff; it does not change stock. Inventory Staff must
        reconcile issued quantities.
      </p>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {message && (
        <p role="status" className="success">
          {message}
        </p>
      )}
      {parts.length ? (
        <Form
          fields={[
            {
              key: "sparePartId",
              label: "Part used",
              type: "number",
              required: true,
              options: parts.map((p) => ({
                value: String(p.sparePartId),
                label: p.partName,
              })),
            },
            {
              key: "quantity",
              label: "Units used (1–1,000,000)",
              type: "number",
              required: true,
            },
            {
              key: "remarks",
              label: "Usage details",
              type: "textarea",
              required: true,
              maxLength: 1000,
            },
          ]}
          submit="Send usage handoff"
          onSubmit={async (value) => {
            const result = await api(
              `work-orders/${workOrderId}/parts-handoff`,
              "POST",
              value,
            );
            setMessage(result.message);
            await onSaved();
          }}
        />
      ) : (
        <p>No parts are available to select.</p>
      )}
    </section>
  );
}
