"use client";

import Image from "next/image";
import { useEffect, useRef, useState } from "react";

/** Licensed decorative branding photography; never customer service evidence. */
export function ServiceHero() {
  const [failed, setFailed] = useState(false);
  const [playing, setPlaying] = useState(false);
  const videoRef = useRef<HTMLVideoElement>(null);

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;
    const motion = window.matchMedia("(prefers-reduced-motion: reduce)");
    let visible = false;
    let disposed = false;
    let playbackFailed = false;
    const eligible = () =>
      visible && !motion.matches && !document.hidden && !playbackFailed;
    const update = () => {
      video.autoplay = eligible();
      if (!eligible()) {
        video.pause();
        return;
      }
      video.muted = true;
      // No video request during SSR, reduced motion or before viewport visibility.
      if (!video.hasAttribute("src"))
        video.src = "/videos/serviceops-login.mp4";
      void video.play().catch((error: unknown) => {
        if (
          disposed ||
          !eligible() ||
          (error instanceof DOMException && error.name === "AbortError")
        )
          return;
        playbackFailed = true;
        video.autoplay = false;
        video.pause();
        setPlaying(false);
      });
    };
    const onError = () => {
      playbackFailed = true;
      update();
      setPlaying(false);
    };
    const observer =
      typeof IntersectionObserver !== "undefined"
        ? new IntersectionObserver(([entry]) => {
            visible = entry.isIntersecting;
            update();
          })
        : null;
    if (observer) observer.observe(video.parentElement!);
    else {
      const bounds = video.getBoundingClientRect();
      visible = bounds.bottom > 0 && bounds.top < window.innerHeight;
      update();
    }
    motion.addEventListener("change", update);
    document.addEventListener("visibilitychange", update);
    video.addEventListener("error", onError);
    return () => {
      disposed = true;
      observer?.disconnect();
      motion.removeEventListener("change", update);
      document.removeEventListener("visibilitychange", update);
      video.removeEventListener("error", onError);
      video.autoplay = false;
      video.pause();
    };
  }, []);
  return (
    <div className="service-hero">
      <div className="service-hero-media service-photo">
        {!failed ? (
          <Image
            src="/branding/field-technician.webp"
            alt="Technician in safety equipment using a tablet during an industrial inspection"
            width={1200}
            height={798}
            sizes="(max-width: 650px) calc(100vw - 68px), 450px"
            onError={() => setFailed(true)}
          />
        ) : (
          <div
            className="photo-placeholder"
            role="img"
            aria-label="Decorative orange gradient; photograph unavailable"
          />
        )}
        <video
          ref={videoRef}
          autoPlay
          muted
          loop
          playsInline
          preload="metadata"
          poster="/branding/field-technician.webp"
          aria-hidden="true"
          tabIndex={-1}
          onPlaying={() => setPlaying(true)}
          onPause={() => setPlaying(false)}
          style={{
            position: "absolute",
            inset: 0,
            width: "100%",
            height: "100%",
            objectFit: "cover",
            pointerEvents: "none",
            opacity: playing ? 1 : 0,
          }}
        />
        <span className="service-photo-overlay" aria-hidden="true" />
      </div>
    </div>
  );
}
