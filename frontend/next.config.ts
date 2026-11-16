import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Serve local assets directly: the Services deployment does not resolve /_next/image.
  images: { unoptimized: true },
  async headers() {
    const development = process.env.NODE_ENV !== "production";
    const policy = [
      "default-src 'self'",
      "base-uri 'self'",
      "object-src 'none'",
      "frame-ancestors 'none'",
      "form-action 'self'",
      "img-src 'self' data: blob:",
      "font-src 'self' data:",
      "style-src 'self' 'unsafe-inline'",
      `script-src 'self' 'unsafe-inline'${development ? " 'unsafe-eval'" : ""}`,
      `connect-src 'self'${development ? " ws: wss:" : ""}`,
      "frame-src 'self' blob:",
    ].join("; ");
    return [
      {
        source: "/(login|dashboard|api)/:path*",
        headers: [{ key: "X-Robots-Tag", value: "noindex, nofollow, noarchive" }],
      },
      {
        source: "/:path*",
        headers: [
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "X-Frame-Options", value: "DENY" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          {
            key: "Permissions-Policy",
            value: "camera=(), microphone=(), geolocation=()",
          },
          { key: "Content-Security-Policy", value: policy },
        ],
      },
      {
        source: "/api/backend/attachments/:id",
        headers: [
          { key: "X-Frame-Options", value: "SAMEORIGIN" },
          {
            key: "Content-Security-Policy",
            value: "sandbox; frame-ancestors 'self'",
          },
        ],
      },
    ];
  },
  turbopack: { root: process.cwd() },
  outputFileTracingRoot: process.cwd(),
};

export default nextConfig;
