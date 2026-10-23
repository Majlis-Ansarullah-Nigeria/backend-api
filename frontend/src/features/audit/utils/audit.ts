/** How an action's chip is toned — always shown with the action's name, never colour alone. */
export type AuditTone = "submitted" | "acknowledged" | "returned" | "late" | "in-review" | "draft" | "upcoming";

export function auditTone(action: string): AuditTone {
  if (action === "report.submitted") return "submitted";
  if (action === "report.acknowledged") return "acknowledged";
  if (action === "report.returned" || action === "report.withdrawn" || action === "auth.sign_in_failed" || action === "user.deactivated") {
    return "returned";
  }
  if (action.startsWith("window.") || action === "report.flagged") return "late";
  if (action.startsWith("template.")) return "in-review";
  if (action === "auth.signed_in") return "upcoming";
  if (action.startsWith("role.") || action.startsWith("unit.") || action.startsWith("user.") || action.startsWith("auth.")) return "acknowledged";
  return "draft";
}

/** "A. Adeyemi"-style initials for the avatar. */
export function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  return (parts[0][0] + (parts.length > 1 ? parts[parts.length - 1][0] : "")).toUpperCase();
}

/** A stored before/after JSON object as "key: value" lines, nested values kept as compact JSON. */
export function jsonLines(json: string | null | undefined): string[] {
  if (!json) return [];
  try {
    const value: unknown = JSON.parse(json);
    if (value === null || typeof value !== "object" || Array.isArray(value)) {
      return [JSON.stringify(value)];
    }
    return Object.entries(value as Record<string, unknown>).map(
      ([key, entry]) => `${key}: ${entry === null ? "—" : typeof entry === "object" ? JSON.stringify(entry) : String(entry)}`,
    );
  } catch {
    return [json];
  }
}

export const DATE_RANGES = [7, 30, 90] as const;

/** The start of a "last N days" range, as an ISO string for the API; undefined for all time. */
export function rangeStart(days: number | null, now: Date = new Date()): string | undefined {
  return days == null ? undefined : new Date(now.getTime() - days * 24 * 60 * 60 * 1000).toISOString();
}
