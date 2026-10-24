import { describe, expect, it } from "vitest";
import { createOrgFormatter, DATE_FORMAT_STYLE } from "./format";

// 23:30 UTC on 4 Oct 2026 is 00:30 on 5 Oct in Lagos (UTC+1).
const late = "2026-10-04T23:30:00Z";

describe("createOrgFormatter", () => {
  it("shows dates in the organization's time zone", () => {
    const lagos = createOrgFormatter({ timeZone: "Africa/Lagos", dateFormat: DATE_FORMAT_STYLE.DayMonthYear });
    expect(lagos.date(late)).toBe("5 Oct 2026");
    expect(lagos.dateTime(late)).toBe("5 Oct 2026, 00:30");
    expect(lagos.dayKey(late)).toBe("2026-10-05");
    expect(lagos.hour(late)).toBe(0);
    expect(lagos.offset(late)).toBe("UTC+1");

    const utc = createOrgFormatter({ timeZone: "UTC", dateFormat: DATE_FORMAT_STYLE.DayMonthYear });
    expect(utc.date(late)).toBe("4 Oct 2026");
  });

  it("follows the chosen date format", () => {
    const at = "2026-10-04T09:15:22Z";
    const zone = "Etc/UTC";
    expect(createOrgFormatter({ timeZone: zone, dateFormat: DATE_FORMAT_STYLE.DayMonthYear }).dayMonth(at)).toBe("4 Oct");
    expect(createOrgFormatter({ timeZone: zone, dateFormat: DATE_FORMAT_STYLE.MonthDayYear }).date(at)).toBe("Oct 4, 2026");
    expect(createOrgFormatter({ timeZone: zone, dateFormat: DATE_FORMAT_STYLE.MonthDayYear }).dayMonth(at)).toBe("Oct 4");
    expect(createOrgFormatter({ timeZone: zone, dateFormat: DATE_FORMAT_STYLE.Iso }).date(at)).toBe("2026-10-04");
    expect(createOrgFormatter({ timeZone: zone, dateFormat: DATE_FORMAT_STYLE.Iso }).dateTimeSeconds(at)).toBe("2026-10-04, 09:15:22");
  });

  it("falls back to UTC for an unknown zone", () => {
    expect(createOrgFormatter({ timeZone: "Mars/Olympus", dateFormat: 0 }).timeZone).toBe("UTC");
  });
});
