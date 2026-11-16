import { NextRequest, NextResponse } from "next/server";
const backend =
  process.env.BACKEND_API_URL ||
  process.env.NEXT_PUBLIC_API_BASE_URL ||
  "http://localhost:5093";
const cookieName = "serviceops_session",
  refreshCookie = "serviceops_refresh";
type Tokens = {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  refreshExpiresAt: string;
  userId: number;
  fullName: string;
  email: string;
  role: string;
};
// Coalesce concurrent requests from one browser in this local server process.
const rotations = new Map<
  string,
  { until: number; promise: Promise<Tokens | null> }
>();
async function rotate(raw: string) {
  const previous = rotations.get(raw);
  if (previous && previous.until > Date.now()) return previous.promise;
  for (const [key, value] of rotations)
    if (value.until <= Date.now()) rotations.delete(key);
  const promise = fetch(`${backend}/api/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken: raw }),
    cache: "no-store",
    signal: AbortSignal.timeout(15000),
  }).then(async (r) => (r.ok ? ((await r.json()) as Tokens) : null));
  rotations.set(raw, { until: Date.now() + 5000, promise });
  setTimeout(() => {
    if (rotations.get(raw)?.promise === promise) rotations.delete(raw);
  }, 5000);
  return promise;
}
function cookies(result: NextResponse, data: Tokens, request: NextRequest) {
  result.headers.set("Cache-Control", "no-store");
  const options = {
    httpOnly: true,
    sameSite: "strict" as const,
    secure: request.nextUrl.protocol === "https:",
    path: "/",
  };
  result.cookies.set(cookieName, data.accessToken, {
    ...options,
    expires: new Date(data.expiresAt),
  });
  result.cookies.set(refreshCookie, data.refreshToken, {
    ...options,
    expires: new Date(data.refreshExpiresAt),
  });
}
function clear(result: NextResponse) {
  result.headers.set("Cache-Control", "no-store");
  result.cookies.delete(cookieName);
  result.cookies.delete(refreshCookie);
}
async function boundedBody(request: NextRequest) {
  const limit = 9 * 1024 * 1024;
  if (Number(request.headers.get("content-length")) > limit)
    throw new RangeError();
  const reader = request.body?.getReader();
  if (!reader) return undefined;
  let length = 0;
  const chunks: Uint8Array[] = [];
  while (true) {
    const part = await reader.read();
    if (part.done) break;
    length += part.value.length;
    if (length > limit) {
      await reader.cancel();
      throw new RangeError();
    }
    chunks.push(part.value);
  }
  const bytes = new Uint8Array(length);
  let offset = 0;
  for (const part of chunks) {
    bytes.set(part, offset);
    offset += part.length;
  }
  return bytes;
}
export async function GET(
  r: NextRequest,
  c: { params: Promise<{ path: string[] }> },
) {
  return proxy(r, c);
}
export async function POST(
  r: NextRequest,
  c: { params: Promise<{ path: string[] }> },
) {
  return proxy(r, c);
}
export async function PUT(
  r: NextRequest,
  c: { params: Promise<{ path: string[] }> },
) {
  return proxy(r, c);
}
export async function PATCH(
  r: NextRequest,
  c: { params: Promise<{ path: string[] }> },
) {
  return proxy(r, c);
}
export async function DELETE(
  r: NextRequest,
  c: { params: Promise<{ path: string[] }> },
) {
  return proxy(r, c);
}
function hasSameOrigin(request: NextRequest): boolean {
  // nextUrl can contain the server's bind address (for example 0.0.0.0).
  // Use the incoming Host, not an untrusted forwarded-host header, for CSRF.
  const host = request.headers.get("host");
  const origin = request.headers.get("origin");
  if (!host || !origin || /[\s,/@?#\\]/.test(host)) return false;
  try {
    const expected = new URL(`${request.nextUrl.protocol}//${host}`);
    return (
      ["http:", "https:"].includes(expected.protocol) &&
      expected.pathname === "/" &&
      origin === expected.origin
    );
  } catch {
    return false;
  }
}

async function proxy(
  request: NextRequest,
  context: { params: Promise<{ path: string[] }> },
) {
  const { path } = await context.params,
    route = path.join("/");
  if (path.some((p) => !/^[a-zA-Z0-9-]+$/.test(p)))
    return NextResponse.json({ message: "Invalid route." }, { status: 400 });
  if (
    request.method !== "GET" &&
    !hasSameOrigin(request)
  )
    return NextResponse.json(
      { message: "Invalid request origin." },
      { status: 403 },
    );
  if (route === "auth/refresh")
    return NextResponse.json(
      { message: "Sessions refresh automatically." },
      { status: 405 },
    );
  if (route === "logout" && request.method !== "POST")
    return NextResponse.json(
      { message: "Use POST to sign out." },
      { status: 405 },
    );
  const login = route === "auth/login",
    logout = route === "logout";
  let token = request.cookies.get(cookieName)?.value;
  const refresh = request.cookies.get(refreshCookie)?.value;
  let rotated: Tokens | null = null;
  try {
    const body =
      request.method === "GET" ? undefined : await boundedBody(request);
    if (!login && !token && refresh) {
      rotated = await rotate(refresh);
      token = rotated?.accessToken;
    }
    if (!login && !token) {
      const result = NextResponse.json(
        { message: "Please sign in." },
        { status: 401 },
      );
      result.headers.set("X-ServiceOps-Session-Expired", "true");
      clear(result);
      return result;
    }
    const send = () =>
      fetch(
        `${backend}/api/${logout ? "auth/logout" : route}${request.nextUrl.search}`,
        {
          method: request.method,
          headers: {
            "Content-Type":
              request.headers.get("content-type") || "application/json",
            ...(token && !login ? { Authorization: `Bearer ${token}` } : {}),
          },
          body,
          cache: "no-store",
          signal: AbortSignal.timeout(15000),
        },
      );
    let response = await send();
    if (
      response.status === 401 &&
      response.headers.has("WWW-Authenticate") &&
      !login &&
      refresh &&
      !rotated
    ) {
      rotated = await rotate(refresh);
      if (rotated) {
        token = rotated.accessToken;
        response = await send();
      }
    }
    const raw = await response.arrayBuffer();
    if (login && response.ok) {
      const data = JSON.parse(new TextDecoder().decode(raw)) as Tokens;
      const result = NextResponse.json({
        userId: data.userId,
        fullName: data.fullName,
        email: data.email,
        role: data.role,
        expiresAt: data.expiresAt,
      });
      cookies(result, data, request);
      return result;
    }
    const result =
      logout && (response.ok || response.status === 401)
        ? NextResponse.json({ message: "Signed out." })
        : new NextResponse(response.status === 204 ? null : raw, {
            status: response.status,
            headers: {
              "Content-Type":
                response.headers.get("Content-Type") || "application/json",
              "Cache-Control": "no-store",
              "X-Content-Type-Options": "nosniff",
              "Content-Security-Policy": "sandbox",
              ...(response.headers.get("Content-Disposition")
                ? {
                    "Content-Disposition": response.headers.get(
                      "Content-Disposition",
                    )!,
                  }
                : {}),
            },
          });
    const expired =
      response.status === 401 && response.headers.has("WWW-Authenticate");
    if (expired) result.headers.set("X-ServiceOps-Session-Expired", "true");
    if (logout || expired || (route === "auth/password" && response.ok))
      clear(result);
    else if (rotated) cookies(result, rotated, request);
    return result;
  } catch (error) {
    if (error instanceof RangeError)
      return NextResponse.json(
        { message: "Request exceeds the 9 MiB limit." },
        { status: 413 },
      );
    return NextResponse.json(
      {
        message:
          "The ServiceOps API is unavailable. Check the backend and database.",
      },
      { status: 503 },
    );
  }
}
