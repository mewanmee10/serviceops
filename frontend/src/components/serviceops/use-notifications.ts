"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import { api, type Row } from "@/lib/serviceops-api";
let audio: AudioContext | null = null;
function stored(storage: Storage, key: string) { try { return storage.getItem(key); } catch { return null; } }
function remember(storage: Storage, key: string, value: string) { try { storage.setItem(key, value); } catch { /* Storage may be disabled; the in-memory watermark still deduplicates. */ } }
export function useNotifications(userId: string | number) {
  const [count, setCount] = useState<number | null>(null), [items, setItems] = useState<Row[]>([]);
  const [sound, setSound] = useState(false), [message, setMessage] = useState(""), [error, setError] = useState("");
  const enabled = useRef(false), highest = useRef<number | null>(null), mounted = useRef(false), pending = useRef(false);
  const key = `serviceops.notifications.${userId}`, preference = `${key}.sound`;
  const refresh = useCallback(async () => {
    if (pending.current || document.visibilityState !== "visible") return;
    pending.current = true;
    try {
      const [unread, rows] = await Promise.all([api("notifications/unread-count"), api("notifications?isRead=false")]);
      if (!mounted.current) return;
      const maximum = Math.max(0, ...rows.map((r: Row) => Number(r.notificationId)));
      const remembered = stored(sessionStorage, `${key}.seen`);
      const previous = highest.current ?? (remembered === null ? maximum : Number(remembered));
      highest.current = Math.max(previous, maximum);
      remember(sessionStorage, `${key}.seen`, String(highest.current));
      setCount(unread.count); setItems(rows); setError("");
      if (maximum > previous && enabled.current && document.hasFocus() && document.visibilityState === "visible" && audio?.state === "running") {
        const announced = Number(stored(localStorage, `${key}.announced`) || "0");
        if (maximum > announced) {
          remember(localStorage, `${key}.announced`, String(maximum));
          try {
            const oscillator = audio.createOscillator(), gain = audio.createGain(), now = audio.currentTime;
            oscillator.type = "sine"; oscillator.frequency.setValueAtTime(660, now);
            gain.gain.setValueAtTime(0.001, now); gain.gain.exponentialRampToValueAtTime(0.035, now + 0.015); gain.gain.exponentialRampToValueAtTime(0.001, now + 0.16);
            oscillator.connect(gain); gain.connect(audio.destination); oscillator.start(now); oscillator.stop(now + 0.17);
            oscillator.onended = () => { oscillator.disconnect(); gain.disconnect(); };
          } catch { setMessage("Sound is unavailable. Notifications remain visible."); }
        }
      }
    } catch (e) { if (mounted.current) setError((e as Error).message); }
    finally { pending.current = false; }
  }, [key]);
  useEffect(() => {
    mounted.current = true;
    const initial = stored(localStorage, preference) === "true";
    enabled.current = initial;
    // State follows a persisted preference, initialized from a user-specific local setting.
    Promise.resolve().then(() => { if (mounted.current) setSound(initial); });
    void Promise.resolve().then(refresh);
    const update = () => { void refresh(); };
    const timer = setInterval(update, 30000);
    window.addEventListener("serviceops-notifications-changed", update);
    document.addEventListener("visibilitychange", update);
    return () => { mounted.current = false; clearInterval(timer); window.removeEventListener("serviceops-notifications-changed", update); document.removeEventListener("visibilitychange", update); };
  }, [refresh, preference]);
  async function toggleSound() {
    if (enabled.current) { enabled.current = false; setSound(false); remember(localStorage, preference, "false"); setMessage("Notification sound muted."); return; }
    try {
      audio ??= new AudioContext();
      await audio.resume();
      if (audio.state !== "running") throw new Error("Audio not unlocked");
      enabled.current = true; setSound(true); remember(localStorage, preference, "true");
      setMessage("Sound enabled for new notifications while this window is active.");
    } catch { setMessage("Your browser could not enable sound. Notifications remain visible."); }
  }
  async function read(id?: number) { await api(id ? `notifications/${id}/read` : "notifications/read-all", id ? "PATCH" : "POST", {}); await refresh(); }
  return { count, items, sound, message, error, refresh, toggleSound, read };
}
