import type { MetadataRoute } from "next";
import { publicOrigin } from "@/lib/public-origin";
export default function sitemap(): MetadataRoute.Sitemap {
  const origin = publicOrigin();
  return origin ? [{ url: origin.href, changeFrequency: "monthly", priority: 1 }] : [];
}
