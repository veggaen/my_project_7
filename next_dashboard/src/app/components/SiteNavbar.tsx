"use client";

import Image from "next/image";
import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";
import { usePathname } from "next/navigation";
import { motion } from "framer-motion";

type NavItem = { href: string; label: string };

const NAV_ITEMS: NavItem[] = [
  { href: "/whitepaper", label: "Whitepaper" },
  { href: "/roadmap", label: "Roadmap" },
  { href: "/stats", label: "Stats" },
  { href: "/admin", label: "Admin" },
];

function useHideOnScroll() {
  const lastY = useRef(0);
  const downTicksRef = useRef(0);
  const downDistanceRef = useRef(0);
  const hideTimeoutRef = useRef<number | null>(null);

  const [hidden, setHidden] = useState(false);
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    const onScroll = () => {
      const y = window.scrollY;
      const delta = y - lastY.current;
      setScrolled(y > 8);

      const isDesktop = window.innerWidth >= 768;
      const tickThreshold = isDesktop ? 12 : 8;
      const startYThreshold = isDesktop ? 140 : 80;
      const requiredTicks = isDesktop ? 4 : 3;
      const requiredDistance = isDesktop ? 120 : 80;
      const debounceDelay = isDesktop ? 150 : 100;

      if (Math.abs(delta) > tickThreshold) {
        if (delta > 0) {
          if (y > startYThreshold) {
            downTicksRef.current = Math.min(requiredTicks, downTicksRef.current + 1);
            downDistanceRef.current += delta;

            if (downTicksRef.current >= requiredTicks && downDistanceRef.current > requiredDistance) {
              if (hideTimeoutRef.current) window.clearTimeout(hideTimeoutRef.current);
              hideTimeoutRef.current = window.setTimeout(() => setHidden(true), debounceDelay);
            }
          }
        } else {
          downTicksRef.current = 0;
          downDistanceRef.current = 0;
          if (hideTimeoutRef.current) {
            window.clearTimeout(hideTimeoutRef.current);
            hideTimeoutRef.current = null;
          }
          setHidden(false);
        }

        lastY.current = y;
      }
    };

    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => {
      window.removeEventListener("scroll", onScroll);
      if (hideTimeoutRef.current) window.clearTimeout(hideTimeoutRef.current);
    };
  }, []);

  return { hidden, scrolled };
}

export function SiteNavbar() {
  const pathname = usePathname();
  const { hidden, scrolled } = useHideOnScroll();

  const items = useMemo(() => {
    return NAV_ITEMS.map((item) => ({
      ...item,
      active: pathname === item.href,
    }));
  }, [pathname]);

  return (
    <motion.nav
      className={`fixed z-50 transition-all duration-500 ${
        scrolled ? "left-0 w-full" : "left-1/2 -translate-x-1/2 w-[calc(100%-2rem)] max-w-5xl"
      } ${hidden ? "-translate-y-full opacity-0" : "translate-y-0 opacity-100"}`}
      initial={{ opacity: 0, y: -20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: "easeOut" }}
      style={{ top: scrolled ? 0 : 24 }}
    >
      <motion.div
        layout
        transition={{ type: "spring", stiffness: 220, damping: 28, mass: 0.8 }}
        className={`w-full transition-all duration-500 ease-in-out backdrop-blur-xl border border-white/5 ${
          scrolled
            ? "w-full rounded-none bg-[#0a0a0f]/80 border-x-0 border-t-0 border-b-white/10 py-3"
            : "rounded-full bg-[#111118]/60 shadow-lg shadow-purple-900/5 py-3"
        }`}
      >
        <div className={`relative px-6 md:px-8 flex items-center justify-center md:justify-between ${scrolled ? "max-w-7xl mx-auto" : ""}`}>
          {/* Logo */}
          <Link href="/" className="flex items-center gap-3 group">
            <div className="relative w-8 h-8 md:w-9 md:h-9 transition-transform duration-300 group-hover:scale-110 group-hover:rotate-3">
              <Image
                src="/hexagon_logo.png"
                alt="Vegga Roleplay"
                fill
                className="object-contain drop-shadow-[0_0_10px_rgba(139,92,246,0.3)]"
              />
            </div>
            <span className="font-bold text-lg md:text-xl tracking-tight bg-linear-to-r from-white via-purple-200 to-gray-400 bg-clip-text text-transparent group-hover:to-white transition-all duration-300">
              Vegga Roleplay
            </span>
          </Link>

          {/* Desktop Nav */}
          <div className="hidden md:flex items-center gap-2">
            {items.map((item) => (
              <Link
                key={item.href}
                href={item.href}
                className={`relative px-5 py-2.5 rounded-full text-base font-medium transition-all duration-300 group overflow-hidden ${
                  item.active
                    ? "text-white bg-white/10 shadow-[0_0_15px_rgba(139,92,246,0.1)]"
                    : "text-gray-400 hover:text-white"
                }`}
              >
                <span className="relative z-10">{item.label}</span>
                {!item.active && (
                  <span className="absolute inset-0 bg-white/5 opacity-0 group-hover:opacity-100 transition-opacity duration-300 rounded-full" />
                )}
                {item.active && (
                  <motion.div
                    layoutId="navbar-indicator"
                    className="absolute inset-0 rounded-full border border-white/10 bg-white/5"
                    transition={{ type: "spring", bounce: 0.2, duration: 0.6 }}
                  />
                )}
              </Link>
            ))}
            <a
              href="https://sbox.game"
              target="_blank"
              className="ml-6 btn btn-primary text-base px-6 py-2.5 glow-purple hover:scale-105 transition-transform shadow-lg shadow-purple-500/20"
            >
              Play Now
            </a>
          </div>

          {/* Mobile Menu Button (Placeholder) */}
          <button className="md:hidden p-2 text-gray-400 hover:text-white absolute right-4">
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="3" y1="12" x2="21" y2="12"></line>
              <line x1="3" y1="6" x2="21" y2="6"></line>
              <line x1="3" y1="18" x2="21" y2="18"></line>
            </svg>
          </button>
        </div>
      </motion.div>
    </motion.nav>
  );
}
