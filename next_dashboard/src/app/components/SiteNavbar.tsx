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
      className={`fixed left-0 w-full z-50 transition-all duration-500 ${hidden ? "-translate-y-full opacity-0" : "translate-y-0 opacity-100"}`}
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      transition={{ duration: 0.25 }}
      style={{ top: scrolled ? 0 : 40 }}
    >
      <div
        className={`border-b backdrop-blur-xl ${
          scrolled
            ? "w-full rounded-none border-x-0"
            : "w-[calc(100%-2.5rem)] max-w-440 mx-auto rounded-2xl border shadow-xl"
        }`}
        style={{
          backgroundColor: "rgba(10, 10, 15, 0.70)",
          borderColor: "rgba(139, 92, 246, 0.20)",
        }}
      >
        <div className="px-6 py-4 flex flex-col md:flex-row items-center justify-between gap-4">
          <Link href="/" className="flex items-center gap-3 min-w-fit">
            <Image src="/hexagon_logo.png" alt="Vegga Roleplay" width={34} height={34} priority />
            <span className="text-lg font-semibold gradient-text">Vegga Roleplay</span>
          </Link>

          <div className="flex items-center gap-2 flex-wrap justify-center md:justify-end w-full md:w-auto">
            {items.map((item) => (
              <Link
                key={item.href}
                href={item.href}
                className={`btn px-4 py-2 text-sm md:text-base ${
                  item.active
                    ? "bg-purple-500/15 text-white border-purple-500/30"
                    : "bg-white/0 text-gray-300 border-white/0 hover:bg-white/5 hover:border-white/10"
                }`}
              >
                {item.label}
              </Link>
            ))}
            <a
              href="https://sbox.game"
              target="_blank"
              className="btn btn-primary text-sm md:text-base"
            >
              Play on s&box
            </a>
          </div>
        </div>
      </div>
    </motion.nav>
  );
}
