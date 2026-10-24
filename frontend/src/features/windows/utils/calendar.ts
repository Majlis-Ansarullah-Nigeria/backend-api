import { levelLabel } from "@/features/templates/utils/report-type";
import type { SubmissionWindowSummary } from "@/lib/api/generated/hRSApiV1.schemas";

export type CalendarEventType = "opens" | "due" | "closes";

export interface CalendarEvent {
  date: string;
  type: CalendarEventType;
  label: string;
  windowId: string;
  reportTypeId: string;
}

export interface WindowForCalendar {
  window: SubmissionWindowSummary;
  reportTypeId: string;
  reportTypeName: string;
}

/** The calendar date (in the viewer's local time zone) an ISO timestamp falls on, as "YYYY-MM-DD". */
export function toDateKey(iso: string): string {
  const date = new Date(iso);
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

/** One opens event, one due event per level, and one closes event, per window. dayKey decides which
 * calendar day an instant falls on — pass the organization's (FR-ADM-001); the default is the
 * viewer's local time zone. */
export function buildCalendarEvents(windows: WindowForCalendar[], dayKey: (iso: string) => string = toDateKey): CalendarEvent[] {
  const events: CalendarEvent[] = [];

  for (const { window, reportTypeId, reportTypeName } of windows) {
    events.push({
      date: dayKey(window.opensAt),
      type: "opens",
      label: `${reportTypeName} opens`,
      windowId: window.id,
      reportTypeId,
    });

    for (const due of window.dueDates) {
      events.push({
        date: dayKey(due.dueAt),
        type: "due",
        label: `${levelLabel(due.level)} due · ${reportTypeName}`,
        windowId: window.id,
        reportTypeId,
      });
    }

    events.push({
      date: dayKey(window.closesAt),
      type: "closes",
      label: `${reportTypeName} closes`,
      windowId: window.id,
      reportTypeId,
    });
  }

  return events;
}

export function groupEventsByDate(events: CalendarEvent[]): Map<string, CalendarEvent[]> {
  const map = new Map<string, CalendarEvent[]>();

  for (const event of events) {
    const existing = map.get(event.date);
    if (existing) {
      existing.push(event);
    } else {
      map.set(event.date, [event]);
    }
  }

  return map;
}

/**
 * Monday-first weeks covering the full month, padded with the leading/trailing days from
 * adjacent months needed to fill each row — the "Windows calendar" mockup's grid shape.
 */
export function buildMonthGrid(year: number, month: number): Date[] {
  const firstOfMonth = new Date(year, month, 1);
  const firstWeekday = (firstOfMonth.getDay() + 6) % 7;
  const start = new Date(year, month, 1 - firstWeekday);

  const lastOfMonth = new Date(year, month + 1, 0);
  const lastWeekday = (lastOfMonth.getDay() + 6) % 7;
  const end = new Date(year, month + 1, 6 - lastWeekday);

  const days: Date[] = [];
  const cursor = new Date(start);
  while (cursor <= end) {
    days.push(new Date(cursor));
    cursor.setDate(cursor.getDate() + 1);
  }

  return days;
}
