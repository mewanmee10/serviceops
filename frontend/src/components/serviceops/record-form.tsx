"use client";
import { FormEvent, useState } from "react";
import type { Row } from "@/lib/serviceops-api";
export type Field = {
  key: string;
  label: string;
  type?: string;
  required?: boolean;
  maxLength?: number;
  matches?: string;
  min?: number;
  max?: number;
  step?: string;
  placeholder?: string;
  help?: string;
  options?: { value: string; label: string }[];
};

export function Form({
  fields,
  onSubmit,
  submit = "Save",
  initial = {},
}: {
  fields: Field[];
  onSubmit: (values: Row) => Promise<void>;
  submit?: string;
  initial?: Row;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [visible, setVisible] = useState<Record<string, boolean>>({});
  async function send(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    const values: Row = {};
    for (const f of fields) {
      const v = String(data.get(f.key) || "");
      values[f.key] =
        f.type === "boolean"
          ? v === "true"
          : f.type === "number"
            ? v
              ? Number(v)
              : null
            : f.type === "datetime-local" || f.type === "date"
              ? v
                ? new Date(v).toISOString()
                : null
              : v || null;
    }
    for (const field of fields) {
      if (field.matches && values[field.key] !== values[field.matches]) {
        setError("Passwords must match.");
        return;
      }
    }
    setBusy(true);
    setError("");
    try {
      await onSubmit(values);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <form onSubmit={send} className="form-grid" aria-busy={busy}>
      {fields.map((f) => (
        <label key={f.key}>
          {f.label}
          {f.help && <small id={`help-${f.key}`} className="muted">{f.help}</small>}
          {f.options ? (
            <select
              aria-label={f.label}
              name={f.key}
              aria-describedby={f.help ? `help-${f.key}` : undefined}
              required={f.required}
              defaultValue={String(initial[f.key] ?? "")}
            >
              <option value="">Select…</option>
              {f.options.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          ) : f.type === "textarea" ? (
            <textarea
              aria-label={f.label}
              name={f.key}
              placeholder={f.placeholder}
              aria-describedby={f.help ? `help-${f.key}` : undefined}
              required={f.required}
              maxLength={f.maxLength ?? 4000}
              defaultValue={String(initial[f.key] ?? "")}
            />
          ) : (
            <span
              className={f.type === "password" ? "password-input" : undefined}
            >
              <input
                aria-label={f.label}
                name={f.key}
              placeholder={f.placeholder}
              aria-describedby={f.help ? `help-${f.key}` : undefined}
                type={
                  f.type === "password" && visible[f.key]
                    ? "text"
                    : f.type || "text"
                }
                required={f.required}
                autoComplete={
                  f.type === "password"
                    ? f.key === "currentPassword" || submit === "Sign in"
                      ? "current-password"
                      : "new-password"
                    : f.key === "email" && submit === "Sign in"
                      ? "username"
                      : undefined
                }
                max={f.max}
                step={f.step}
                min={
                  f.min ??
                  (f.type === "number"
                    ? f.key === "delta"
                      ? -1000000
                      : f.key === "lowStockLevel"
                        ? 0
                        : 1
                    : undefined)
                }
                minLength={
                  f.type === "password" &&
                  f.key !== "currentPassword" &&
                  submit !== "Sign in"
                    ? 12
                    : undefined
                }
                maxLength={f.maxLength ?? (f.type === "password" ? 1024 : 500)}
                defaultValue={
                  f.type === "date"
                    ? String(initial[f.key] ?? "").slice(0, 10)
                    : String(initial[f.key] ?? "")
                }
              />
              {f.type === "password" && (
                <button
                  type="button"
                  className="password-visibility"
                  aria-label={`${visible[f.key] ? "Hide" : "Show"} ${f.label.toLowerCase()}`}
                  aria-pressed={Boolean(visible[f.key])}
                  onClick={() =>
                    setVisible((current) => ({
                      ...current,
                      [f.key]: !current[f.key],
                    }))
                  }
                >
                  {visible[f.key] ? "Hide" : "Show"}
                </button>
              )}
            </span>
          )}
        </label>
      ))}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <button className="primary" disabled={busy}>
        {busy ? "Saving…" : submit}
      </button>
    </form>
  );
}
