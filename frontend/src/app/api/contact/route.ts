import { NextRequest, NextResponse } from "next/server";
export async function POST(request: NextRequest) {
  const host = request.headers.get("host");
  if (!host || /[\s,/@?#\\]/.test(host) || request.headers.get("origin") !== `${request.nextUrl.protocol}//${host}`)
    return NextResponse.json({ message: "Invalid request origin." }, { status: 403 });
  if (!request.headers.get("content-type")?.startsWith("application/json"))
    return NextResponse.json({ message: "JSON is required." }, { status: 415 });
  try {
    const reader = request.body?.getReader(); if (!reader) return NextResponse.json({ message: "A request body is required." }, { status: 400 });
    const chunks: Uint8Array[] = []; let size = 0;
    while (true) { const { done, value } = await reader.read(); if (done) break; size += value.length; if (size > 16384) { await reader.cancel(); return NextResponse.json({ message: "Inquiry is too large." }, { status: 413 }); } chunks.push(value); }
    const bytes = new Uint8Array(size); let offset = 0; for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
    let input; try { input = JSON.parse(new TextDecoder().decode(bytes)); } catch { return NextResponse.json({ message: "Invalid JSON." }, { status: 400 }); }
    const backend = process.env.BACKEND_API_URL || process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5093";
    const response = await fetch(`${backend}/api/contact-inquiries`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(input), cache: "no-store", signal: AbortSignal.timeout(15000) });
    const body = await response.text();
    return new NextResponse(body || JSON.stringify({ message: response.status === 429 ? "Too many inquiries. Please try again later." : "Unable to submit inquiry." }), { status: response.status, headers: { "Content-Type": "application/json", "Cache-Control": "no-store" } });
  } catch { return NextResponse.json({ message: "The inquiry service is unavailable. Please try again later." }, { status: 503 }); }
}
