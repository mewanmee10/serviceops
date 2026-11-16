"use client";
import { useEffect, useState } from "react";
import { AdministrationAudit } from "./inventory-audit";
import { api } from "@/lib/serviceops-api";
type Summary = {
  parts: number;
  unitsInStock: number;
  lowStock: {
    sparePartId: number;
    partName: string;
    quantityInStock: number;
    lowStockLevel: number;
  }[];
  movements: { type: number; transactions: number; quantity: number }[];
};
type Transaction = {
  partName: string;
  transactionId: number;
  sparePartId: number;
  workOrderId: number | null;
  transactionType: number;
  quantity: number;
  transactionDate: string;
  remarks: string;
};
const names = [
  "Addition / return",
  "Reduction",
  "Issue",
  "Consumption",
  "Adjustment",
];
export function InventorySummary() {
  const [summary, setSummary] = useState<Summary | null>(null),
    [transactions, setTransactions] = useState<Transaction[]>([]),
    [query, setQuery] = useState(""),
    [error, setError] = useState(""),
    [page, setPage] = useState(1);
  useEffect(() => {
    let live = true;
    Promise.all([
      api(`inventory/summary?${query}`),
      api(`inventory/transactions?${query}`),
    ])
      .then(([s, t]) => {
        if (live) {
          setSummary(s);
          setTransactions(t);
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
        <h2>Inventory summary and transactions</h2>
        <form
          className="form-grid"
          onSubmit={(e) => {
            e.preventDefault();
            const q = new URLSearchParams();
            for (const [k, v] of new FormData(e.currentTarget))
              if (v)
                q.set(
                  k,
                  k === "from" || k === "to"
                    ? String(v) + (k === "to" ? "T23:59:59Z" : "T00:00:00Z")
                    : String(v),
                );
            setQuery(q.toString());
            setPage(1);
          }}
        >
          <label>
            From
            <input type="date" name="from" />
          </label>
          <label>
            To
            <input type="date" name="to" />
          </label>
          <label>
            Movement type
            <select name="type">
              <option value="">All movements</option>
              {names.map((n, i) => (
                <option key={i} value={i}>
                  {n}
                </option>
              ))}
            </select>
          </label>
          <label>
            Part ID
            <input type="number" name="partId" min="1" />
          </label>
          <label>
            Work order ID
            <input type="number" name="workOrderId" min="1" />
          </label>
          <button>Filter transactions</button>
        </form>
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
        <p className="muted">
          Stock totals show current availability. Summary movement totals use
          the selected dates; the transaction table also uses type, part and
          work-order filters.
        </p>
      </section>
      {summary && (
        <>
          <div className="metrics dashboard-metrics">
            <section className="metric">
              <span>Catalog parts</span>
              <strong>{summary.parts}</strong>
            </section>
            <section className="metric">
              <span>Available stock units</span>
              <strong>{summary.unitsInStock}</strong>
            </section>
            <section className="metric">
              <span>Low-stock parts</span>
              <strong>{summary.lowStock.length}</strong>
            </section>
          </div>
          <section className="panel">
            <h2>Low-stock monitoring</h2>
            {summary.lowStock.map((p) => (
              <p key={p.sparePartId}>
                #{p.sparePartId} {p.partName} · {p.quantityInStock} available ·
                threshold {p.lowStockLevel}
              </p>
            ))}
            {!summary.lowStock.length && <p>No low-stock parts.</p>}
            <h2>Movement summary</h2>
            {summary.movements.map((m) => (
              <p key={m.type}>
                {names[m.type]}: {m.transactions} transactions · {m.quantity}{" "}
                units
              </p>
            ))}
          </section>
        </>
      )}
      <section className="panel">
        <h2>Filtered movement history</h2>
        <div
          className="table-scroll"
          tabIndex={0}
          role="region"
          aria-label="Stock transactions"
        >
          <table>
            <thead>
              <tr>
                <th>Date</th>
                <th>Part</th>
                <th>Work order</th>
                <th>Type</th>
                <th>Quantity</th>
                <th>Reason</th>
              </tr>
            </thead>
            <tbody>
              {transactions.slice((page - 1) * 25, page * 25).map((t) => (
                <tr key={t.transactionId}>
                  <td>{new Date(t.transactionDate).toLocaleString()}</td>
                  <td>{t.partName} <small>#{t.sparePartId}</small></td>
                  <td>{t.workOrderId ?? "—"}</td>
                  <td>{names[t.transactionType]}</td>
                  <td>{t.quantity}</td>
                  <td>{t.remarks}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {transactions.length === 0 && <p>No matching transactions.</p>}
        <div className="pagination">
          <button disabled={page === 1} onClick={() => setPage((p) => p - 1)}>
            Previous transactions
          </button>
          <span>
            {transactions.length} movements · page {page}
          </span>
          <button
            disabled={page * 25 >= transactions.length}
            onClick={() => setPage((p) => p + 1)}
          >
            Next transactions
          </button>
        </div>
      </section>
      <AdministrationAudit />
    </>
  );
}
