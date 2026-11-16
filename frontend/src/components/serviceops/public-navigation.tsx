"use client";

import Link from "next/link";
import { Menu, ArrowRight, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Brand } from "./brand";
import styles from "./landing.module.css";

const links = [["Home", "home"], ["Features", "features"], ["How It Works", "workflow"], ["Platform", "modules"], ["Contact", "contact"]] as const;

/** Only the public navigation is interactive; landing content remains server-rendered. */
export function PublicNavigation() {
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState("home");
  const header = useRef<HTMLElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    const observer = new IntersectionObserver(entries => {
      for (const entry of entries) if (entry.isIntersecting) setActive(entry.target.id);
    }, { rootMargin: "-100px 0px -55% 0px" });
    for (const [, id] of links) { const section = document.getElementById(id); if (section) observer.observe(section); }
    return () => observer.disconnect();
  }, []);
  useEffect(() => {
    if (!open) return;
    header.current?.querySelector<HTMLAnchorElement>(`#public-mobile-menu a`)?.focus();
    const outside = (event: PointerEvent) => {
      if (!header.current?.contains(event.target as Node)) setOpen(false);
    };
    const resize = () => { if (window.innerWidth > 900) setOpen(false); };
    document.addEventListener("pointerdown", outside);
    window.addEventListener("resize", resize);
    return () => { document.removeEventListener("pointerdown", outside); window.removeEventListener("resize", resize); };
  }, [open]);
  const navigate = (id: string) => {
    setActive(id); setOpen(false);
    const destination = document.getElementById(id);
    destination?.setAttribute("tabindex", "-1");
    destination?.focus({ preventScroll: true });
  };
  return (
    <header ref={header} className={`${styles.header} glass-navbar`} onKeyDown={event => {
      if (event.key === "Escape" && open) { event.preventDefault(); setOpen(false); trigger.current?.focus(); }
      if (event.key === "Tab" && open) {
        const items = [trigger.current, ...Array.from(header.current?.querySelectorAll<HTMLAnchorElement>("#public-mobile-menu a") ?? [])].filter(Boolean) as HTMLElement[];
        // Explicit movement also supports WebKit configurations that skip links on Tab.
        const index = items.findIndex(item => item === document.activeElement);
        if (items.length) {
          event.preventDefault();
          items[(index + (event.shiftKey ? -1 : 1) + items.length) % items.length].focus();
        }
      }
    }}>
      <Link href="/" aria-label="ServiceOps home"><Brand /></Link>
      <nav className={styles.desktopNav} aria-label="Public navigation">
        {links.map(([label, id]) => <a key={id} href={`#${id}`} aria-current={active === id ? "location" : undefined} onClick={() => navigate(id)}>{label}</a>)}
      </nav>
      <div className={styles.headerActions}>
        <Link href="/login" className={styles.smallButton}>Sign In <ArrowRight size={15} /></Link>
        <button ref={trigger} className={styles.menuToggle} aria-expanded={open} aria-controls="public-mobile-menu" onClick={() => setOpen(!open)}>
          {open ? <X size={18} aria-hidden /> : <Menu size={18} aria-hidden />}<span>Menu</span>
        </button>
      </div>
      <nav id="public-mobile-menu" hidden={!open} className={`${styles.mobileMenu} glass-panel`} aria-label="Mobile public navigation">
        {links.map(([label, id]) => <a key={id} href={`#${id}`} aria-current={active === id ? "location" : undefined} onClick={() => navigate(id)}>{label}</a>)}
        <Link href="/login">Sign In</Link>
      </nav>
    </header>
  );
}
