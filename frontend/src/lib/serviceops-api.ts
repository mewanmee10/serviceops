export type Row = Record<string, string | number | boolean | null>;

export class ApiError extends Error { constructor(message: string, public readonly status: number) { super(message); this.name = "ApiError"; } }

export async function api(path: string, method = "GET", body?: object) {
  const r = await fetch(`/api/backend/${path}`, {
    method,
    headers: { "Content-Type": "application/json" },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (
    r.status === 401 &&
    r.headers.get("X-ServiceOps-Session-Expired") === "true"
  )
    window.dispatchEvent(new Event("session-expired"));
  const data = r.status === 204 ? null : await r.json().catch(() => null);
  if (!r.ok)
    throw new ApiError(
      data?.message ||
        (data?.errors
          ? Object.values(data.errors).flat().join(" ")
          : data?.title) ||
        `Request failed (${r.status}).`, r.status,
    );
  if (r.ok && method !== "GET" && path.startsWith("notifications")) window.dispatchEvent(new Event("serviceops-notifications-changed"));
  return data;
}
