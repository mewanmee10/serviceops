"use client";

import Image from "next/image";
import { useCallback, useEffect, useRef } from "react";
import styles from "./logo-intro.module.css";

/** Decorative login branding. No session identifiers or tokens are stored here. */
export function LogoIntro({ onDismiss }: { onDismiss: () => void }) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const skipRef = useRef<HTMLButtonElement>(null);
  const closing = useRef(false);
  const dismissed = useRef(false);

  const complete = useCallback(() => {
    if (dismissed.current) return;
    dismissed.current = true;
    videoRef.current?.pause();
    dialogRef.current?.close();
    onDismiss();
  }, [onDismiss]);

  const finish = useCallback((fade = true) => {
    if (dismissed.current) return;
    if (closing.current) { if (!fade) complete(); return; }
    closing.current = true;
    videoRef.current?.pause();
    if (!fade || window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      complete();
      return;
    }
    dialogRef.current?.classList.add(styles.closing);
  }, [complete]);

  useEffect(() => {
    const dialog = dialogRef.current;
    const video = videoRef.current;
    if (!dialog || !video) return;
    const motion = window.matchMedia("(prefers-reduced-motion: reduce)");
    let disposed = false;
    const previousFocus = document.activeElement;
    if (motion.matches) {
      complete();
      return;
    }
    dialog.showModal(); // Native modal isolates dashboard interaction and traps focus.
    skipRef.current?.focus();
    video.muted = true;
    video.autoplay = !document.hidden;
    video.src = "/videos/serviceops-logo-intro.mp4";
    const play = () => {
      if (disposed || closing.current || document.hidden) return;
      void video.play().catch(() => {
        if (!disposed && !closing.current && !document.hidden) finish(false);
      });
    };
    const visibility = () => {
      video.autoplay = !document.hidden;
      if (document.hidden) video.pause(); else play();
    };
    const preference = () => { if (motion.matches) finish(false); };
    document.addEventListener("visibilitychange", visibility);
    motion.addEventListener("change", preference);
    play();
    return () => {
      disposed = true;
      document.removeEventListener("visibilitychange", visibility);
      motion.removeEventListener("change", preference);
      video.pause();
      video.removeAttribute("src");
      video.load();
      dialog.close();
      if (previousFocus instanceof HTMLElement && previousFocus.isConnected) previousFocus.focus();
      else document.getElementById("workspace-content")?.focus();
    };
  }, [complete, finish]);

  return (
    <dialog
      ref={dialogRef}
      className={styles.intro}
      aria-labelledby="logo-intro-title"
      aria-describedby="logo-intro-description"
      onKeyDown={(event) => {
        // Skip is the sole interactive control; retain both Tab directions.
        if (event.key === "Tab") { event.preventDefault(); skipRef.current?.focus(); }
      }}
      onCancel={(event) => { event.preventDefault(); finish(); }}
      onAnimationEnd={(event) => {
        if (event.target === event.currentTarget && closing.current) complete();
      }}
    >
      <h2 id="logo-intro-title" className={styles.title}>Welcome to ServiceOps</h2>
      <p id="logo-intro-description" className={styles.description}>
        A brief brand animation. Your dashboard is loading in the background.
      </p>
      <div className={styles.frame}>
        <Image
          className={styles.fallback}
          src="/branding/serviceops-logo.png"
          alt=""
          width={280}
          height={280}
          sizes="(max-width: 600px) 160px, 280px"
        />
        <video
          ref={videoRef}
          className={styles.video}
          autoPlay
          muted
          playsInline
          preload="auto"
          poster="/branding/serviceops-logo.png"
          aria-hidden="true"
          tabIndex={-1}
          onEnded={() => finish()}
          onError={() => finish(false)}
          onStalled={() => finish(false)}
        />
      </div>
      <button ref={skipRef} type="button" className={styles.skip} onClick={() => finish()}>
        Skip animation
      </button>
    </dialog>
  );
}
