import type { MetadataRoute } from "next";
import { publicOrigin } from "@/lib/public-origin";
export default function robots(): MetadataRoute.Robots {
  const origin = publicOrigin();
  return {
    rules: { userAgent: "*", allow: "/", disallow: ["/login", "/dashboard", "/api/"] },
    ...(origin ? { sitemap: new URL("/sitemap.xml", origin).href } : {}),
  };
}
