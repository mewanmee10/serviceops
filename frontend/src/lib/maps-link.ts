/** External navigation only: no SDK, API key, geocoding or server-side URL resolution. */
export function safeMapsUrl(value: unknown): string | null {
  if (typeof value !== "string" || !value.trim() || value.trim().length > 2048 || /[\x00-\x1f\x7f]/.test(value)) return null;
  try {
    const u = new URL(value.trim());
    if (u.protocol !== "https:" || u.username || u.password || u.port) return null;
    const host = u.hostname.toLowerCase(), p = u.pathname;
    const valid = ["google.com", "www.google.com"].includes(host) ? p === "/maps" || p.startsWith("/maps/")
      : host === "maps.google.com" || (host === "maps.app.goo.gl" && p.length > 1) || (host === "goo.gl" && p.startsWith("/maps/"));
    return valid ? u.href : null;
  } catch { return null; }
}
export function directionsUrl(record: Record<string, unknown>): string | null {
  const saved = safeMapsUrl(record.mapsUrl);
  if (saved) return saved;
  const destination = [record.addressLine, record.siteCity ?? record.city, record.siteProvince ?? record.province]
    .filter(v => typeof v === "string" && v.trim()).join(", ");
  return destination ? `https://www.google.com/maps/dir/?${new URLSearchParams({ api: "1", destination })}` : null;
}
