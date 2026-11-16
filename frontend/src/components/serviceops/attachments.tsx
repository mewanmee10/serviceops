"use client";
import Image from "next/image";
import { useEffect, useState } from "react";
import { api } from "@/lib/serviceops-api";
const maximumMiB = Math.min(8, Math.max(1, Number(process.env.NEXT_PUBLIC_UPLOAD_MAX_MIB) || 8));
type Evidence = {
  attachmentId: number;
  fileName: string;
  fileType: string;
  uploadedAt: string;
};
export function Attachments({
  resource,
  id,
}: {
  resource: "requests" | "work-orders";
  id: number;
}) {
  const [failedImages, setFailedImages] = useState<Set<number>>(new Set());
  const [items, setItems] = useState<Evidence[]>([]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const path = `${resource}/${id}/attachments`;
  useEffect(() => {
    let alive = true;
    api(path)
      .then((data) => {
        if (alive) setItems(data);
      })
      .catch((e) => {
        if (alive) setError(e.message);
      });
    return () => {
      alive = false;
    };
  }, [path]);
  async function upload(file?: File) {
    if (!file) return;
    if (
      file.size > maximumMiB * 1024 * 1024 ||
      !["image/png", "image/jpeg", "application/pdf"].includes(file.type)
    ) {
      setError(`Select a PNG, JPEG or PDF up to ${maximumMiB} MiB.`);
      return;
    }
    setBusy(true);
    setError("");
    try {
      const form = new FormData();
      form.append("file", file);
      const response = await fetch(`/api/backend/${path}`, {
        method: "POST",
        body: form,
      });
      const data = await response.json();
      if (!response.ok)
        throw new Error(data.message || data.title || "Upload failed.");
      setItems(await api(path));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="evidence">
      <h3>Service evidence</h3>
      <p className="muted small">
        PNG, JPEG or PDF · Maximum {maximumMiB} MiB per file · Private to authorized
        service participants
      </p>
      <label className="file-upload">
        Upload evidence
        <input
          type="file"
          aria-label="Upload evidence"
          accept="image/png,image/jpeg,application/pdf"
          disabled={busy}
          onChange={(e) => {
            void upload(e.target.files?.[0]);
            e.target.value = "";
          }}
        />
      </label>
      {busy && <p role="status">Uploading…</p>}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <div className="evidence-grid">
        {items.map((file) => (
          <article key={file.attachmentId}>
            {file.fileType.startsWith("image/") &&
              !failedImages.has(file.attachmentId) && (
                <Image
                  unoptimized
                  onError={() =>
                    setFailedImages((current) =>
                      new Set(current).add(file.attachmentId),
                    )
                  }
                  src={`/api/backend/attachments/${file.attachmentId}?preview=true`}
                  width={280}
                  height={170}
                  alt={file.fileName}
                  style={{ objectFit: "contain", maxWidth: "100%" }}
                />
              )}
            {failedImages.has(file.attachmentId) && (
              <p className="muted small">
                Preview unavailable. Download the evidence to view it.
              </p>
            )}
            {file.fileType === "application/pdf" && (
              <a
                target="_blank"
                rel="noopener noreferrer"
                href={`/api/backend/attachments/${file.attachmentId}?preview=true`}
              >
                Preview PDF
              </a>
            )}
            <a href={`/api/backend/attachments/${file.attachmentId}`}>
              {file.fileName}
            </a>
            <small>{new Date(file.uploadedAt).toLocaleString()}</small>
          </article>
        ))}
      </div>
      {!items.length && <p className="muted small">No evidence uploaded.</p>}
    </section>
  );
}
