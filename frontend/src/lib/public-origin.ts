/** Set only after a public deployment origin has been approved. Never infer it from requests. */
export function publicOrigin(): URL | undefined {
  const value = process.env.SERVICEOPS_PUBLIC_ORIGIN;
  if (!value) return undefined;
  try {
    const url = new URL(value);
    if (url.protocol !== "https:" || url.username || url.password || url.pathname !== "/" || url.search || url.hash || url.hostname === "localhost") return undefined;
    return url;
  } catch {
    return undefined;
  }
}
