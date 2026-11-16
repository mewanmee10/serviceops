"use client";
import { useEffect, useState } from "react";
import { api } from "@/lib/serviceops-api";

type MonitoringData = {
  checkedAt: string;
  databaseReachable: boolean;
  activeAccounts: number;
  inactiveAccounts: number;
  unscheduledJobs: number;
  overdueJobs: number;
  recentActivity: {
    auditLogId: number;
    action: string;
    entityType: string;
    entityId: number | null;
    createdAt: string;
  }[];
};
export function Monitoring() {
  const [data, setData] = useState<MonitoringData | null>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    let live = true;
    api("administration/monitoring")
      .then((value) => {
        if (live) setData(value);
      })
      .catch((e) => {
        if (live) setError(e.message);
      });
    return () => {
      live = false;
    };
  }, []);
  return (
    <section className="panel">
      <h2>Administrative operation snapshot</h2>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {!data && !error && (
        <p role="status">Checking current database records…</p>
      )}
      {data && (
        <>
          <p>
            Checked {new Date(data.checkedAt).toLocaleString()} · Database{" "}
            {data.databaseReachable ? "reachable" : "unavailable"}
          </p>
          <div className="metrics">
            {[
              ["Active accounts", data.activeAccounts],
              ["Inactive accounts", data.inactiveAccounts],
              ["Unscheduled jobs", data.unscheduledJobs],
              ["Overdue jobs", data.overdueJobs],
            ].map(([label, count]) => (
              <section className="metric" key={label}>
                <span>{label}</span>
                <strong>{count}</strong>
              </section>
            ))}
          </div>
          <h3>Recent system activity</h3>
          {data.recentActivity.map((a) => (
            <p key={a.auditLogId}>
              {new Date(a.createdAt).toLocaleString()} · {a.action} ·{" "}
              {a.entityType} {a.entityId ?? ""}
            </p>
          ))}
          {!data.recentActivity.length && <p>No recorded activity.</p>}
          <p className="muted">
            A current operational snapshot, not an uptime or performance
            monitor. Refresh to check again.
          </p>
        </>
      )}
    </section>
  );
}
