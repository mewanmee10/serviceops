"use client";
import { useRef, useState } from "react";
export function ContactSection() {
  const submission = useRef<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState("");
  return <section id="contact" className="contact-section glass-panel" aria-labelledby="contact-heading">
    <div><p className="muted">LET’S CONNECT</p><h2 id="contact-heading">Contact ServiceOps</h2><p>Speak to us about your service and maintenance needs.</p><div className="contact-cards"><a href="mailto:contact@serviceops.com">contact@serviceops.com</a><a href="tel:+94777744333">+94 77 774 4333</a></div><p className="muted">Inquiries are stored securely for Administrator review. Email delivery is not currently configured.</p></div>
    <form onSubmit={async event => {
      event.preventDefault(); if (busy) return;
      const form = event.currentTarget; const values = Object.fromEntries(new FormData(form));
      submission.current ??= crypto.randomUUID(); setBusy(true); setFeedback("");
      try {
        const response = await fetch("/api/contact", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ ...values, phoneNumber: String(values.phoneNumber || "").trim() || null, privacyAcknowledged: values.privacyAcknowledged === "on", submissionId: submission.current }) });
        const body = await response.json();
        if (!response.ok) throw new Error(body.message || Object.values(body.errors || {}).flat().join(" ") || `Unable to submit inquiry (${response.status}).`);
        setFeedback(body.message); form.reset(); submission.current = null;
      } catch (error) { setFeedback(error instanceof Error ? error.message : "Unable to submit. Please try again."); }
      finally { setBusy(false); }
    }}>
      <label>Full name<input name="fullName" autoComplete="name" required maxLength={100}/></label>
      <label>Email address<input name="email" type="email" autoComplete="email" required maxLength={254}/></label>
      <label>Phone number (optional)<input name="phoneNumber" type="tel" autoComplete="tel" maxLength={32}/></label>
      <label>Subject<input name="subject" required maxLength={200}/></label>
      <label>Message<textarea name="message" required maxLength={4000} rows={5}/></label>
      <div hidden aria-hidden="true"><label>Website<input name="website" tabIndex={-1} autoComplete="off"/></label></div>
      <label className="contact-consent"><input name="privacyAcknowledged" type="checkbox" required/>I agree that ServiceOps may use these details to respond to this inquiry.</label>
      <button type="submit" disabled={busy}>{busy ? "Submitting…" : "Send inquiry"}</button>
      <p role="status" aria-live="polite">{feedback}</p>
    </form>
  </section>;
}
