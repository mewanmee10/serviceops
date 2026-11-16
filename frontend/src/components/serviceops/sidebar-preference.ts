"use client";
import { useSyncExternalStore } from "react";
const key = "serviceops_sidebar";
const change = "serviceops-sidebar-change";
let fallback = false;
function snapshot() {
  try {
    return localStorage.getItem(key) === "collapsed";
  } catch {
    return fallback;
  }
}
function subscribe(callback: () => void) {
  window.addEventListener(change, callback);
  window.addEventListener("storage", callback);
  return () => {
    window.removeEventListener(change, callback);
    window.removeEventListener("storage", callback);
  };
}
export function useSidebarPreference() {
  const collapsed = useSyncExternalStore(subscribe, snapshot, () => false);
  function toggle() {
    fallback = !collapsed;
    try {
      localStorage.setItem(key, fallback ? "collapsed" : "expanded");
    } catch {
      /* Keep the current tab usable when persistence is unavailable. */
    }
    window.dispatchEvent(new Event(change));
  }
  return [collapsed, toggle] as const;
}
