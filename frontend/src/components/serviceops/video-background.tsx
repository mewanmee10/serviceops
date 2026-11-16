"use client";

import { useEffect, useRef } from "react";

/** Nonessential decoration, mounted only inside the authenticated workspace. */
export function ServiceOpsMaintenanceBackground({ paused = false }: { paused?: boolean }) {
  const videoRef = useRef<HTMLVideoElement>(null);

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;
    const motion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const mobile = window.matchMedia("(max-width: 640px)");
    const connection = (navigator as Navigator & {
      connection?: EventTarget & { saveData?: boolean; effectiveType?: string };
    }).connection;
    let disposed = false;
    let failed = false;
    const allowed = () => !paused && !motion.matches && !mobile.matches &&
      !connection?.saveData && !["slow-2g", "2g"].includes(connection?.effectiveType ?? "");
    const fallback = () => {
      if (disposed || failed) return;
      failed = true;
      video.pause();
      video.hidden = true;
      video.removeAttribute("src");
      video.load();
    };
    const sync = () => {
      if (disposed) return;
      if (!allowed() || document.hidden || failed) {
        video.pause();
        if (!allowed()) {
          video.hidden = true;
          if (video.hasAttribute("src")) {
            video.removeAttribute("src");
            video.load();
          }
        }
        return;
      }
      if (!video.hasAttribute("src")) video.src = "/videos/serviceops-maintenance-bg-optimized.mp4";
      video.muted = true;
      video.play().then(() => {
        if (disposed) return;
        if (document.hidden || !allowed()) video.pause();
        else video.hidden = false;
      }).catch(() => {
        // Visibility/preference changes can abort a pending play without a media failure.
        if (!disposed && !document.hidden && allowed()) fallback();
      });
    };
    video.addEventListener("error", fallback);
    motion.addEventListener("change", sync);
    mobile.addEventListener("change", sync);
    connection?.addEventListener("change", sync);
    document.addEventListener("visibilitychange", sync);
    sync();
    return () => {
      disposed = true;
      video.removeEventListener("error", fallback);
      motion.removeEventListener("change", sync);
      mobile.removeEventListener("change", sync);
      connection?.removeEventListener("change", sync);
      document.removeEventListener("visibilitychange", sync);
      video.pause();
      video.hidden = true;
      video.removeAttribute("src");
      video.load();
    };
  }, [paused]);

  return (
    <div className="serviceops-video-background fixed inset-0 pointer-events-none" aria-hidden="true">
      <video ref={videoRef} hidden autoPlay muted loop playsInline preload="metadata"
        poster="/images/serviceops-maintenance-poster.webp" aria-hidden="true" tabIndex={-1} />
      <div className="serviceops-video-dimmer" />
    </div>
  );
}
