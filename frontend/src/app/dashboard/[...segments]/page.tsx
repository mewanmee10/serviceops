import { notFound } from "next/navigation";
import Workspace from "@/components/serviceops/workspace";
import { recordResources } from "@/lib/record-routes";
export const metadata = { title: "Record | ServiceOps", robots: {index:false,follow:false,noarchive:true} };
export default async function RecordPage({params}: {params:Promise<{segments:string[]}>}) {
 const {segments}=await params;const [resource,raw,action]=segments;
 if (!recordResources[resource] || !/^[1-9]\d*$/.test(raw ?? "") || Number(raw)>2147483647 || segments.length>3 || (action && action!=="edit")) notFound();
 return <Workspace key={segments.join("/")} initialSection={resource} recordRoute={{resource,id:Number(raw),edit:action==="edit"}}/>;
}
