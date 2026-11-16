import type { Metadata } from "next";
import Workspace from "@/components/serviceops/workspace";
export const metadata: Metadata = { title: { absolute: "Workspace | ServiceOps" }, robots: { index: false, follow: false, noarchive: true } };
export default async function DashboardPage({searchParams}: {searchParams: Promise<{section?: string}>}) {
  const {section = "dashboard"} = await searchParams;
  return <Workspace key={section} initialSection={section} />;
}
