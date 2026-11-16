import type { Metadata } from "next";
import { Landing } from "@/components/serviceops/landing";
import { publicOrigin } from "@/lib/public-origin";

const origin = publicOrigin();
const title = "Field Service & Maintenance Management";
const description = "Bring customers, service managers, technicians and inventory teams together. Manage service requests, equipment, work orders and maintenance history with ServiceOps.";
export const metadata: Metadata = {
  title: { absolute: `${title} | ServiceOps` },
  description,
  robots: { index: true, follow: true },
  ...(origin ? { metadataBase: origin, alternates: { canonical: origin.href } } : {}),
  openGraph: {
    title: `${title} | ServiceOps`,
    description,
    siteName: "ServiceOps",
    type: "website",
    locale: "en_US",
    ...(origin ? { url: origin.href, images: [{ url: new URL("/previews/operations-light.webp", origin).href, alt: "ServiceOps service operations dashboard preview" }] } : {}),
  },
  twitter: { card: "summary", title: `${title} | ServiceOps`, description },
};
export default function Home() {
  return <Landing />;
}
