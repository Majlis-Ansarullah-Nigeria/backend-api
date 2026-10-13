function isSameCalendarDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

/** The mockup's two section headers: "Today" for anything created on today's calendar date
 * (local time), "Earlier" for everything before — a simple, deliberately coarse split. */
export function notificationDayGroup(createdAt: string, now: Date = new Date()): "today" | "earlier" {
  return isSameCalendarDay(new Date(createdAt), now) ? "today" : "earlier";
}

/** Per-row timestamp label matching the mockup: time-of-day today, "Yesterday" for yesterday,
 * "D MMM" otherwise. */
export function formatNotificationTimestamp(createdAt: string, now: Date = new Date()): string {
  const created = new Date(createdAt);

  if (isSameCalendarDay(created, now)) {
    return created.toLocaleTimeString("en-GB", { hour: "numeric", minute: "2-digit" });
  }

  const yesterday = new Date(now);
  yesterday.setDate(yesterday.getDate() - 1);
  if (isSameCalendarDay(created, yesterday)) {
    return "Yesterday";
  }

  return created.toLocaleDateString("en-GB", { day: "numeric", month: "short" });
}
