"use client";

import { useEffect } from "react";

function isExtensionNoise(value: unknown): boolean {
  if (!value) return false;

  const text = typeof value === "string" ? value : JSON.stringify(value);

  // Common patterns for injected extension scripts.
  if (text.includes("chrome-extension://")) return true;
  if (text.includes("inpage.js")) return true;

  return false;
}

export function ExtensionNoiseSilencer() {
  useEffect(() => {
    const onError = (event: ErrorEvent) => {
      if (isExtensionNoise(event.message) || isExtensionNoise(event.error?.stack)) {
        event.preventDefault();
      }
    };

    const onUnhandledRejection = (event: PromiseRejectionEvent) => {
      const reason = event.reason as any;
      if (isExtensionNoise(reason) || isExtensionNoise(reason?.message) || isExtensionNoise(reason?.stack)) {
        event.preventDefault();
      }
    };

    window.addEventListener("error", onError);
    window.addEventListener("unhandledrejection", onUnhandledRejection);
    return () => {
      window.removeEventListener("error", onError);
      window.removeEventListener("unhandledrejection", onUnhandledRejection);
    };
  }, []);

  return null;
}
