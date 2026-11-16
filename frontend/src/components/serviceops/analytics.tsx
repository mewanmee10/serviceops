"use client";
import { useEffect, useState } from "react";
import { api } from "@/lib/serviceops-api";
type AnalyticsData = {
  from: string;
  to: string;
  openRequests: number;
  completedJobs: number;
  overdueJobs: number;
  averageCompletionHours: number | null;
  averageResponseHours: number | null;
  frequentlyFailingAssets: {
    assetId: number;
    assetName: string;
    requestCount: number;
  }[];
  averageFeedback: number | null;
  feedbackCount: number;
  priorities: { priority: number; count: number }[];
  monthlyCompletions: { month: string; count: number }[];
  technicians: {
    technicianId: number;
    name: string;
    activeJobs: number;
    completedJobs: number;
    averageRating: number | null;
  }[];
  stockMovements: { month: string; type: number; quantity: number }[] | null;
  lowStock:
    | { partName: string; quantityInStock: number; lowStockLevel: number }[]
    | null;
};
const movementNames = [
  "Addition / return",
  "Reduction",
  "Issue",
  "Consumption",
  "Adjustment",
];
export function Analytics() {
  const [data, setData] = useState<AnalyticsData | null>(null),
    [query, setQuery] = useState(""),
    [error, setError] = useState("");
  useEffect(() => {
    let live = true;
    api(`analytics?${query}`)
      .then((d) => {
        if (live) {
          setData(d);
          setError("");
        }
      })
      .catch((e) => {
        if (live) setError(e.message);
      });
    return () => {
      live = false;
    };
  }, [query]);
  return (
    <>
      <section className="panel">
        <h2>Service analytics</h2>
        <form
          className="form-grid"
          onSubmit={(e) => {
            e.preventDefault();
            const q = new URLSearchParams();
            for (const [k, v] of new FormData(e.currentTarget))
              if (v)
                q.set(
                  k,
                  String(v) + (k === "to" ? "T23:59:59Z" : "T00:00:00Z"),
                );
            setQuery(q.toString());
          }}
        >
          <label>
            From
            <input name="from" type="date" />
          </label>
          <label>
            To
            <input name="to" type="date" />
          </label>
          <button>Apply dates</button>
        </form>
        <p className="muted">
          Scoped to your accessible records. Completion trends use completion
          date; workload uses request creation date. Duration includes time on
          hold.
        </p>
        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}
      </section>
      {!data ? (
        <p role="status">Loading analytics…</p>
      ) : (
        <>
          <div className="metrics">
            {[
              ["Open requests", data.openRequests],
              ["Completed jobs", data.completedJobs],
              ["Overdue jobs", data.overdueJobs],
              [
                "Average completion hours",
                data.averageCompletionHours ?? "No completed data",
              ],
              [
                "Average response hours",
                data.averageResponseHours ?? "No started jobs",
              ],
              ["Average feedback", data.averageFeedback ?? "No feedback"],
            ].map(([label, value]) => (
              <section className="metric" key={label}>
                <span>{label}</span>
                <strong>{value}</strong>
              </section>
            ))}
          </div>
          <section className="panel">
            <h2>Frequently failing assets</h2>
            <p className="muted">
              Ranked by service request count in the selected period. Requests
              indicate reported issues, not confirmed equipment failures.
              Response hours measure request creation to first job start; time
              on hold is included in completion hours.
            </p>
            <Bars
              values={data.frequentlyFailingAssets.map((asset) => ({
                label: asset.assetName,
                value: asset.requestCount,
              }))}
            />
            <h2>Monthly service completions</h2>
            <Bars
              values={data.monthlyCompletions.map((m) => ({
                label: m.month,
                value: m.count,
              }))}
            />
            <h2>Request priorities</h2>
            <Bars
              values={data.priorities.map((p) => ({
                label: ["Low", "Medium", "High", "Urgent"][p.priority],
                value: p.count,
              }))}
            />
          </section>
          <section className="panel">
            <h2>Technician workload and feedback</h2>
            <div
              className="table-scroll"
              tabIndex={0}
              role="region"
              aria-label="Technician analytics"
            >
              <table>
                <thead>
                  <tr>
                    <th>Technician</th>
                    <th>Active</th>
                    <th>Completed</th>
                    <th>Average rating</th>
                  </tr>
                </thead>
                <tbody>
                  {data.technicians.map((t) => (
                    <tr key={t.technicianId}>
                      <td>{t.name}</td>
                      <td>{t.activeJobs}</td>
                      <td>{t.completedJobs}</td>
                      <td>{t.averageRating ?? "No feedback"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
          {data.stockMovements && (
            <section className="panel">
              <h2>
                {data.lowStock === null
                  ? "Spare-parts consumption"
                  : "Stock movement trends"}
              </h2>
              <Bars
                values={data.stockMovements.map((m) => ({
                  label: `${m.month} ${movementNames[m.type]}`,
                  value: m.quantity,
                }))}
              />
              {data.lowStock !== null && <h2>Low-stock report</h2>}
              {data.lowStock?.map((p, index) => (
                <p key={`${p.partName}-${index}`}>
                  {p.partName}: {p.quantityInStock} available · threshold{" "}
                  {p.lowStockLevel}
                </p>
              ))}
              {data.lowStock?.length === 0 && <p>No low-stock parts.</p>}
            </section>
          )}
        </>
      )}
    </>
  );
}
function Bars({ values }: { values: { label: string; value: number }[] }) {
  const max = Math.max(1, ...values.map((v) => Math.abs(v.value)));
  return values.length === 0 ? (
    <p className="muted">No records in this period.</p>
  ) : (
    <div
      className="chart"
      role="img"
      aria-label={values.map((v) => `${v.label}: ${v.value}`).join(", ")}
    >
      {values.map((v, i) => (
        <div className="bar-row" key={i}>
          <span>{v.label}</span>
          <div className="bar-track">
            <div
              className="bar-fill"
              style={{ width: `${(Math.abs(v.value) / max) * 100}%` }}
            />
          </div>
          <strong>{v.value}</strong>
        </div>
      ))}
    </div>
  );
}
