const BROWSERS: Array<[pattern: RegExp, name: string]> = [
  [/Edg\//, "Edge"],
  [/Chrome\//, "Chrome"],
  [/CriOS\//, "Chrome"],
  [/Firefox\//, "Firefox"],
  [/Safari\//, "Safari"],
];

const PLATFORMS: Array<[pattern: RegExp, name: string]> = [
  [/iPhone|iPad/, "iPhone"],
  [/Android/, "Android"],
  [/Mac OS X/, "Mac"],
  [/Windows/, "Windows"],
  [/Linux/, "Linux"],
];

export function isMobileUserAgent(userAgent: string | null): boolean {
  return userAgent !== null && /iPhone|iPad|Android|Mobile/.test(userAgent);
}

/** A light heuristic parse — good enough for "Chrome on Windows", not a full UA database. */
export function formatDeviceLabel(userAgent: string | null, fallback: string): string {
  if (!userAgent) {
    return fallback;
  }

  const browser = BROWSERS.find(([pattern]) => pattern.test(userAgent))?.[1];
  const platform = PLATFORMS.find(([pattern]) => pattern.test(userAgent))?.[1];

  if (browser && platform) {
    return `${browser} on ${platform}`;
  }

  return browser ?? platform ?? fallback;
}

export function formatRelativeTime(isoDate: string): string {
  const formatter = new Intl.RelativeTimeFormat("en", { numeric: "auto" });
  const diffMs = new Date(isoDate).getTime() - Date.now();

  const units: Array<[Intl.RelativeTimeFormatUnit, number]> = [
    ["year", 1000 * 60 * 60 * 24 * 365],
    ["month", 1000 * 60 * 60 * 24 * 30],
    ["day", 1000 * 60 * 60 * 24],
    ["hour", 1000 * 60 * 60],
    ["minute", 1000 * 60],
  ];

  for (const [unit, ms] of units) {
    const value = diffMs / ms;
    if (Math.abs(value) >= 1) {
      return formatter.format(Math.round(value), unit);
    }
  }

  return formatter.format(0, "minute");
}
