"use client";

import { useEffect, useState } from "react";

/**
 * Ticks down from `seconds` to 0, once per second. To restart it (e.g. after a resend),
 * remount the component that calls this hook via a changing `key` prop — not a hook
 * argument, since resetting state from a changing value belongs in render, not an effect.
 */
export function useCountdown(seconds: number): number {
  const [remaining, setRemaining] = useState(seconds);

  useEffect(() => {
    const interval = setInterval(() => {
      setRemaining((current) => (current <= 1 ? 0 : current - 1));
    }, 1000);

    return () => clearInterval(interval);
  }, []);

  return remaining;
}
