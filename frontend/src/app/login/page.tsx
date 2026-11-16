import type { Metadata } from "next";
import Workspace from "@/components/serviceops/workspace";
export const metadata: Metadata = { title: { absolute: "Sign in | ServiceOps" }, robots: { index: false, follow: false, noarchive: true } };
export default function LoginPage() {
  return <Workspace />;
}
