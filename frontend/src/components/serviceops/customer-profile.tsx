"use client";

import { Building2, Pencil } from "lucide-react";
import type { Row } from "@/lib/serviceops-api";
import styles from "./customer-profile.module.css";

/** The parent selects the authenticated user's record from the scoped API. */
export function CustomerProfile({
  customer,
  loading,
  onEdit,
}: {
  customer?: Row;
  loading: boolean;
  onEdit: () => void;
}) {
  if (loading)
    return (
      <section
        className="panel"
        role="status"
        aria-label="Loading customer details"
      >
        Loading your customer details…
      </section>
    );
  if (!customer)
    return (
      <section className="panel" aria-label="My customer details">
        <h2>Your customer profile is not linked</h2>
        <p className="muted">
          Contact your service team to link your customer record. No other
          customer records are shown.
        </p>
      </section>
    );
  const text = (key: string) =>
    String(customer[key] ?? "").trim() || "Not provided";
  const fields = [
    ["email", "Email address"],
    ["phoneNumber", "Contact phone"],
    ["addressLine", "Address"],
    ["city", "City"],
    ["province", "Province"],
    ["postalCode", "Postal code"],
  ];
  return (
    <section
      className={`panel ${styles.card}`}
      aria-label="My customer details"
    >
      <div className={styles.header}>
        <div className={styles.identity}>
          <span className={styles.avatar} aria-hidden="true">
            <Building2 size={28} />
          </span>
          <div>
            <p className="eyebrow">YOUR CUSTOMER PROFILE</p>
            <h2>{text("customerName")}</h2>
            <p className="muted">
              Your details help the service team coordinate visits and repairs.
            </p>
          </div>
        </div>
        <button className="primary" onClick={onEdit}>
          <Pencil size={16} /> Edit my details
        </button>
      </div>
      <dl className={styles.details}>
        {fields.map(([key, label]) => (
          <div key={key}>
            <dt>{label}</dt>
            <dd>{text(key)}</dd>
          </div>
        ))}
      </dl>
      <div className={styles.notes}>
        <h3>Customer notes</h3>
        <p>
          {String(customer.notes ?? "").trim() || "No customer notes added."}
        </p>
        <p className="muted small">
          Shared with your authorized service team. Update your account name
          below; password settings are in Profile &amp; Security.
        </p>
      </div>
    </section>
  );
}
