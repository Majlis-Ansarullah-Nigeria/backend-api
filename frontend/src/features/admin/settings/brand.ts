// Matches backend/src/HRS.Domain/Admin/BrandColor.cs — order and colours. Every option keeps
// white text on the fill and the dark shade as text on white at WCAG AA.
export const BRAND_COLORS = [
  { value: 0, key: "green", primary: "#0B7A4B" },
  { value: 1, key: "teal", primary: "#0E7490" },
  { value: 2, key: "blue", primary: "#1D4ED8" },
  { value: 3, key: "purple", primary: "#7C3AED" },
  { value: 4, key: "orange", primary: "#B45309" },
] as const;

/** "18.4 MB" — binary units, one decimal. */
export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ["KB", "MB", "GB", "TB"];
  let value = bytes / 1024;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return `${value.toFixed(1)} ${units[unit]}`;
}

export const LOGO_MAX_BYTES = 512 * 1024;
