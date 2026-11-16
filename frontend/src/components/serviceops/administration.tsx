"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { api } from "@/lib/serviceops-api";
type Permission = {
  permissionId: number;
  permissionName: string;
  description: string;
};
type Role = { roleId: number; roleName: string; permissionIds: number[] };
type Audit = {
  auditLogId: number;
  actor: string;
  actorRole: string;
  action: string;
  entityType: string;
  entityId: number | null;
  createdAt: string;
};
export function Administration({
  mode = "both",
}: {
  mode?: "permissions" | "audit" | "both";
}) {
  const [roles, setRoles] = useState<Role[]>([]),
    [permissions, setPermissions] = useState<Permission[]>([]),
    [audit, setAudit] = useState<Audit[]>([]),
    [total, setTotal] = useState(0),
    [page, setPage] = useState(1),
    [filters, setFilters] = useState(""),
    [error, setError] = useState(""),
    [message, setMessage] = useState("");
  useEffect(() => {
    if (mode === "audit") return;
    Promise.all([
      api("administration/roles"),
      api("administration/permissions"),
    ])
      .then(([r, p]) => {
        setRoles(r);
        setPermissions(p);
      })
      .catch((e) => setError(e.message));
  }, [mode]);
  useEffect(() => {
    if (mode === "permissions") return;
    let live = true;
    api(`administration/audit?page=${page}&${filters}`)
      .then((d) => {
        if (live) {
          setAudit(d.items);
          setTotal(d.total);
        }
      })
      .catch((e) => {
        if (live) setError(e.message);
      });
    return () => {
      live = false;
    };
  }, [page, filters, mode]);
  async function save(role: Role) {
    try {
      await api(`administration/roles/${role.roleId}/permissions`, "PUT", {
        permissionIds: role.permissionIds,
      });
      setMessage(`Permissions saved for ${role.roleName}.`);
      setError("");
    } catch (e) {
      setError((e as Error).message);
    }
  }
  return (
    <>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {mode !== "audit" && (
        <section className="panel">
          <h2>Roles and permissions</h2>
          <p className="muted">
            Five fixed project roles. Permission checks apply immediately.
            Essential Administrator access is protected by the API.
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
          <div className="permission-grid">
            {roles.map((role) => (
              <fieldset key={role.roleId}>
                <legend>{role.roleName}</legend>
                {permissions.map((p) => (
                  <label className="check-label" key={p.permissionId}>
                    <input
                      type="checkbox"
                      checked={role.permissionIds.includes(p.permissionId)}
                      onChange={(e) =>
                        setRoles((current) =>
                          current.map((r) =>
                            r.roleId !== role.roleId
                              ? r
                              : {
                                  ...r,
                                  permissionIds: e.target.checked
                                    ? [...r.permissionIds, p.permissionId]
                                    : r.permissionIds.filter(
                                        (id) => id !== p.permissionId,
                                      ),
                                },
                          ),
                        )
                      }
                    />
                    <span>
                      {p.permissionName}
                      <small>{p.description}</small>
                    </span>
                  </label>
                ))}
                <button onClick={() => save(role)}>Save permissions</button>
              </fieldset>
            ))}
          </div>
        </section>
      )}
      {mode !== "permissions" && (
        <section className="panel">
          <h2>Audit trail</h2>
          <form
            className="form-grid"
            onSubmit={(e) => {
              e.preventDefault();
              const values = new FormData(e.currentTarget),
                q = new URLSearchParams();
              for (const [k, v] of values)
                if (v)
                  q.set(
                    k,
                    k === "from" || k === "to"
                      ? String(v) + (k === "to" ? "T23:59:59Z" : "T00:00:00Z")
                      : String(v),
                  );
              setFilters(q.toString());
              setPage(1);
            }}
          >
            <label>Search<input name="search" maxLength={200} /></label>
            <label>Order<select name="sort"><option value="desc">Newest first</option><option value="asc">Oldest first</option></select></label>
            <label>
              Entity type
              <input name="entityType" placeholder="WorkOrder" />
            </label>
            <label>
              Action contains
              <input name="action" />
            </label>
            <label>
              From
              <input type="date" name="from" />
            </label>
            <label>
              To
              <input type="date" name="to" />
            </label>
            <button>Filter audit</button>
          </form>
          <div
            className="table-scroll"
            tabIndex={0}
            role="region"
            aria-label="Audit records"
          >
            <table>
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Actor</th>
                  <th>Role</th><th>Action</th>
                  <th>Entity</th><th>Details</th>
                </tr>
              </thead>
              <tbody>
                {audit.map((a) => (
                  <tr key={a.auditLogId}>
                    <td>{new Date(a.createdAt).toLocaleString()}</td>
                    <td>{a.actor}</td>
                    <td>{a.actorRole}</td><td>{a.action}</td>
                    <td>
                      {a.entityType} {a.entityId ?? ""}
                    </td><td><Link href={`/dashboard/audit/${a.auditLogId}`}>View details</Link></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {audit.length === 0 && <p>No audit entries match.</p>}
          <div className="table-toolbar">
            <button disabled={page === 1} onClick={() => setPage((p) => p - 1)}>
              Previous audit page
            </button>
            <span>
              {total} entries · page {page}
            </span>
            <button
              disabled={page * 25 >= total}
              onClick={() => setPage((p) => p + 1)}
            >
              Next audit page
            </button>
          </div>
        </section>
      )}
    </>
  );
}
