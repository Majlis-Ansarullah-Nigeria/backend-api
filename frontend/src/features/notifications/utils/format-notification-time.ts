import { createOrgFormatter, DEFAULT_ORG_FORMAT, type OrgFormatter } from "@/lib/org/format";

const DEFAULT_FORMAT = createOrgFormatter(DEFAULT_ORG_FORMAT);

/** Calendar days in the organization's time zone (FR-ADM-001), not the browser's. */
function isSameCalendarDay(a: Date, b: Date, fmt: OrgFormatter): boolean {
  return fmt.dayKey(a) === fmt.dayKey(b);
}

/** The mockup's two section headers: "Today" for anything created on today's calendar date
 * (organization time), "Earlier" for everything before — a simple, deliberately coarse split. */
export function notificationDayGroup(createdAt: string, now: Date = new Date(), fmt: OrgFormatter = DEFAULT_FORMAT): "today" | "earlier" {
  return isSameCalendarDay(new Date(createdAt), now, fmt) ? "today" : "earlier";
}

/** Per-row timestamp label matching the mockup: time-of-day today, "Yesterday" for yesterday,
 * "D MMM" otherwise. */
export function formatNotificationTimestamp(createdAt: string, now: Date = new Date(), fmt: OrgFormatter = DEFAULT_FORMAT): string {
  const created = new Date(createdAt);

  if (isSameCalendarDay(created, now, fmt)) {
    return fmt.time(created);
  }

  const yesterday = new Date(now.getTime() - 24 * 60 * 60 * 1000);
  if (isSameCalendarDay(created, yesterday, fmt)) {
    return "Yesterday";
  }

  return fmt.dayMonth(created);
}
