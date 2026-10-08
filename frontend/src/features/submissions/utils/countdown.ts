/** Midnight UTC of the given date's calendar day — so "today/tomorrow" compares calendar days,
 * not raw 24-hour windows (a due date later today must still read "Due today", not "tomorrow"). */
function startOfUtcDay(date: Date): number {
  return Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());
}

/**
 * A short, human countdown string against a due date (FR-SUB-001's "a countdown"). Always
 * computed against the live client clock, never cached — "now" changes while the page sits open.
 */
export function countdownText(dueAt: string, now: Date = new Date()): string {
  const due = new Date(dueAt);
  const diffMs = due.getTime() - now.getTime();
  const diffDays = Math.round((startOfUtcDay(due) - startOfUtcDay(now)) / (24 * 60 * 60 * 1000));

  if (diffMs < 0) {
    const overdueDays = Math.abs(diffDays);
    return overdueDays === 0 ? "Overdue today" : overdueDays === 1 ? "Overdue by 1 day" : `Overdue by ${overdueDays} days`;
  }

  if (diffDays === 0) {
    return "Due today";
  }

  if (diffDays === 1) {
    return "Due tomorrow";
  }

  return `${diffDays} days left`;
}
