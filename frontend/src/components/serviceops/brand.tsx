import Image from "next/image";

export function Brand() {
  return (
    <div className="brand" aria-label="ServiceOps">
      <span className="brand-emblem" aria-hidden="true">
        <Image
          src="/branding/serviceops-logo.png"
          alt=""
          width={128}
          height={128}
          sizes="128px"
        />
      </span>
      <span>
        Service<span className="brand-accent">Ops</span>
        <small>FIELD SERVICE & MAINTENANCE</small>
      </span>
    </div>
  );
}
