"use client";
import { useEffect, useState } from "react";
import { api } from "@/lib/serviceops-api";

type Audit = {
  auditLogId: number;
  actor: string;
  actorRole: string;
  action: string;
  entityType: string;
  entityId: number | null;
  createdAt: string;
};
export function AdministrationAudit() {
  const [filters,setFilters]=useState("");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<{ total: number; items: Audit[] } | null>(
    null,
  );
  const [error, setError] = useState("");
  useEffect(() => {
    let live = true;
    api(`inventory/audit?page=${page}&${filters}`)
      .then((value) => {
        if (live) {
          setData(value);
          setError("");
        }
      })
      .catch((e) => {
        if (live) setError(e.message);
      });
    return () => {
      live = false;
    };
  }, [page,filters]);
  return (
    <section className="panel">
      <h2>Inventory audit information</h2>
      <p className="muted">
        Only spare-part and stock-transaction changes are included.
      </p>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {!data && !error && <p role="status">Loading inventory audit…</p>}
      <form className="form-grid" onSubmit={e=>{e.preventDefault();const q=new URLSearchParams();for(const[k,v]of new FormData(e.currentTarget))if(v)q.set(k,(k==='from'||k==='to')?String(v)+(k==='to'?'T23:59:59Z':'T00:00:00Z'):String(v));setFilters(q.toString());setPage(1)}}>
        <label>Search<input name="search" maxLength={200}/></label><label>From<input name="from" type="date"/></label><label>To<input name="to" type="date"/></label><label>Order<select name="sort"><option value="desc">Newest first</option><option value="asc">Oldest first</option></select></label><button>Filter inventory audit</button>
      </form>
      <div className="table-scroll" role="region" aria-label="Inventory audit records" tabIndex={0}><table><thead><tr><th>Date</th><th>Actor</th><th>Role</th><th>Action</th><th>Entity</th></tr></thead><tbody>{data?.items.map(a=><tr key={a.auditLogId}><td>{new Date(a.createdAt).toLocaleString()}</td><td>{a.actor}</td><td>{a.actorRole}</td><td>{a.action}</td><td>{a.entityType} #{a.entityId ?? '—'}</td></tr>)}</tbody></table></div>
      {data && !data.items.length && <p>No inventory changes recorded.</p>}
      <div className="pagination">
        <button disabled={page === 1} onClick={() => setPage((p) => p - 1)}>
          Previous inventory audit
        </button>
        <span>
          Page {page} · {data?.total ?? 0} changes
        </span>
        <button
          disabled={!data || page * 25 >= data.total}
          onClick={() => setPage((p) => p + 1)}
        >
          Next inventory audit
        </button>
      </div>
    </section>
  );
}
