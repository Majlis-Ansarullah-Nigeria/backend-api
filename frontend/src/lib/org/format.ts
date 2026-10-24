// Matches backend/src/HRS.Domain/Admin/DateFormatStyle.cs's enum order.
export const DATE_FORMAT_STYLE = {
  DayMonthYear: 0,
  MonthDayYear: 1,
  Iso: 2,
} as const;

export interface OrgFormatSettings {
  timeZone: string;
  dateFormat: number;
}

/** What the app shows until the settings load, and outside the app shell (FR-ADM-001 defaults). */
export const DEFAULT_ORG_FORMAT: OrgFormatSettings = { timeZone: "Africa/Lagos", dateFormat: DATE_FORMAT_STYLE.DayMonthYear };

type DateInput = Date | string | number;

const toDate = (value: DateInput) => (value instanceof Date ? value : new Date(value));

/**
 * FR-ADM-001: every date the app shows, in the organization's time zone and date format. Times are
 * stored in UTC; this is the only place they're converted for display.
 */
export function createOrgFormatter(settings: OrgFormatSettings) {
  const timeZone = safeTimeZone(settings.timeZone);
  const style = settings.dateFormat;
  const make = (locale: string, options: Intl.DateTimeFormatOptions) => new Intl.DateTimeFormat(locale, { ...options, timeZone });

  const fullDate =
    style === DATE_FORMAT_STYLE.Iso
      ? make("en-CA", { year: "numeric", month: "2-digit", day: "2-digit" })
      : style === DATE_FORMAT_STYLE.MonthDayYear
        ? make("en-US", { month: "short", day: "numeric", year: "numeric" })
        : make("en-GB", { day: "numeric", month: "short", year: "numeric" });
  const dayMonth =
    style === DATE_FORMAT_STYLE.Iso
      ? fullDate
      : style === DATE_FORMAT_STYLE.MonthDayYear
        ? make("en-US", { month: "short", day: "numeric" })
        : make("en-GB", { day: "numeric", month: "short" });
  const time = make("en-GB", { hour: "2-digit", minute: "2-digit", hourCycle: "h23" });
  const seconds = make("en-GB", { hour: "2-digit", minute: "2-digit", second: "2-digit", hourCycle: "h23" });
  const weekday = make("en-GB", { weekday: "long" });
  const monthYear = make("en-GB", { month: "long", year: "numeric" });
  const monthShort = make("en-GB", { month: "short" });
  const day = make("en-GB", { day: "numeric" });
  const hour = make("en-GB", { hour: "numeric", hourCycle: "h23" });
  const ymd = make("en-CA", { year: "numeric", month: "2-digit", day: "2-digit" });
  const offset = make("en-GB", { timeZoneName: "shortOffset" });
  // A calendar day ("2026-10-04") is not an instant: format it as-is, never shifted by a zone.
  const calendarDay = new Intl.DateTimeFormat(fullDate.resolvedOptions().locale, {
    ...(style === DATE_FORMAT_STYLE.Iso
      ? { year: "numeric", month: "2-digit", day: "2-digit" }
      : { day: "numeric", month: "short", year: "numeric" }),
    timeZone: "UTC",
  } as Intl.DateTimeFormatOptions);

  return {
    timeZone,
    /** 4 Oct 2026 · Oct 4, 2026 · 2026-10-04 */
    date: (value: DateInput) => fullDate.format(toDate(value)),
    /** 4 Oct · Oct 4 (the full ISO date when the format is ISO) */
    dayMonth: (value: DateInput) => dayMonth.format(toDate(value)),
    /** 4 Oct 2026, 09:15 */
    dateTime: (value: DateInput) => `${fullDate.format(toDate(value))}, ${time.format(toDate(value))}`,
    /** 4 Oct, 09:15 */
    dayMonthTime: (value: DateInput) => `${dayMonth.format(toDate(value))}, ${time.format(toDate(value))}`,
    /** 4 Oct 2026, 09:15:22 */
    dateTimeSeconds: (value: DateInput) => `${fullDate.format(toDate(value))}, ${seconds.format(toDate(value))}`,
    /** 09:15 */
    time: (value: DateInput) => time.format(toDate(value)),
    /** Monday, 4 Oct 2026 */
    weekdayDate: (value: DateInput) => `${weekday.format(toDate(value))}, ${fullDate.format(toDate(value))}`,
    /** October 2026 */
    monthYear: (value: DateInput) => monthYear.format(toDate(value)),
    /** Oct */
    monthShort: (value: DateInput) => monthShort.format(toDate(value)),
    /** 4 */
    dayOfMonth: (value: DateInput) => day.format(toDate(value)),
    /** 0–23 in the organization's time zone */
    hour: (value: DateInput) => Number(hour.format(toDate(value))),
    /** 2026-10-04 — the calendar day in the organization's time zone, for comparing days */
    dayKey: (value: DateInput) => ymd.format(toDate(value)),
    /** A calendar-day key ("2026-10-04") in the chosen format, without any time-zone shift. */
    calendarDate: (key: string) => calendarDay.format(new Date(`${key}T00:00:00Z`)),
    /** GMT+1 — the organization's offset at that moment */
    offset: (value: DateInput) =>
      offset.formatToParts(toDate(value)).find((part) => part.type === "timeZoneName")?.value.replace("GMT", "UTC") ?? "UTC",
  };
}

export type OrgFormatter = ReturnType<typeof createOrgFormatter>;

/** An unknown or blank zone falls back to UTC rather than throwing at render time. */
function safeTimeZone(zone: string): string {
  try {
    new Intl.DateTimeFormat("en", { timeZone: zone });
    return zone;
  } catch {
    return "UTC";
  }
}
