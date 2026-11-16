import { useId } from "react";

/** Authored decorative service-card illustration; contains no customer or job data. */
export function ServiceArtwork() {
  const id = useId();
  return (
    <svg
      className="service-artwork"
      viewBox="0 0 240 260"
      aria-hidden="true"
      focusable="false"
    >
      <defs>
        <linearGradient id={id} x1="0" y1="0" x2="1" y2="1">
          <stop stopColor="var(--brand-orange)" />
          <stop offset="1" stopColor="var(--brand-deep)" />
        </linearGradient>
      </defs>
      <ellipse
        cx="119"
        cy="231"
        rx="77"
        ry="14"
        fill="var(--foreground)"
        opacity=".08"
      />
      <path
        d="M46 57 173 32 205 61 197 205 71 226 39 199Z"
        fill="var(--secondary)"
        stroke="var(--border)"
        strokeWidth="2"
      />
      <path
        d="m39 49 126-23 32 25-9 146-125 23-32-27Z"
        fill="var(--card)"
        stroke="var(--border)"
        strokeWidth="2"
      />
      <path d="m54 50 94-17 19 15-1 21-94 17-19-15Z" fill={`url(#${id})`} />
      <path
        d="m62 103 94-17M61 126l71-13M60 149l81-15"
        fill="none"
        stroke="var(--muted-foreground)"
        strokeWidth="8"
        strokeLinecap="round"
        opacity=".3"
      />
      <path
        d="m60 180 11 8 20-26"
        fill="none"
        stroke="var(--brand-deep)"
        strokeWidth="9"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle
        cx="178"
        cy="169"
        r="43"
        fill={`url(#${id})`}
        stroke="var(--card)"
        strokeWidth="6"
      />
      <path
        d="m166 175-16 16a7 7 0 0 0 10 10l16-17a20 20 0 0 0 23-27l-12 12-10-10 12-12a20 20 0 0 0-23 28Z"
        fill="var(--primary-foreground)"
      />
      <circle cx="157" cy="195" r="2.5" fill="var(--brand-deep)" />
      <path
        d="m200 40 4-12m-13 5 12 5m-170 97 4-10m-12 4 11 5"
        stroke="var(--brand-orange)"
        strokeWidth="3"
        strokeLinecap="round"
        opacity=".6"
      />
    </svg>
  );
}
