import { describe, expect, it } from "vitest";
import { UNIT_LEVEL_CODE } from "@/features/templates/utils/report-type";
import type { SubmissionWindowSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { buildCalendarEvents, buildMonthGrid, groupEventsByDate, toDateKey } from "./calendar";

const REMINDER_DEFAULTS = {
  remindFiveDaysBefore: true,
  remindTwoDaysBefore: true,
  remindOnDueDay: true,
  escalateToParentAfterThreeDays: true,
  escalateToZoneAfterSevenDays: false,
};

function window(overrides: Partial<SubmissionWindowSummary> = {}): SubmissionWindowSummary {
  return {
    id: "window-1",
    reportTypeId: "report-type-1",
    periodLabel: "October 2026",
    opensAt: "2026-10-01T12:00:00Z",
    graceDays: 3,
    closesAt: "2026-10-25T12:00:00Z",
    dueDates: [{ level: UNIT_LEVEL_CODE.Muqam, dueAt: "2026-10-05T12:00:00Z" }],
    createdAt: "2026-09-01T00:00:00Z",
    scheduleId: null,
    targetUnitIds: [],
    extensions: [],
    reopens: [],
    reminderSettings: REMINDER_DEFAULTS,
    ...overrides,
  };
}

describe("toDateKey", () => {
  it("extracts the calendar date from an ISO timestamp", () => {
    expect(toDateKey("2026-10-05T12:00:00Z")).toBe("2026-10-05");
  });
});

describe("buildCalendarEvents", () => {
  it("emits one opens, one due per level, and one closes event per window", () => {
    const events = buildCalendarEvents([{ window: window(), reportTypeId: "report-type-1", reportTypeName: "Monthly" }]);

    expect(events).toHaveLength(3);
    expect(events.filter((e) => e.type === "opens")).toHaveLength(1);
    expect(events.filter((e) => e.type === "due")).toHaveLength(1);
    expect(events.filter((e) => e.type === "closes")).toHaveLength(1);
  });

  it("labels a due event with the level and the report type name", () => {
    const events = buildCalendarEvents([{ window: window(), reportTypeId: "report-type-1", reportTypeName: "Monthly" }]);

    const due = events.find((e) => e.type === "due");
    expect(due?.label).toBe("Muqam due · Monthly");
    expect(due?.date).toBe("2026-10-05");
  });

  it("emits one due event per level when a window has several", () => {
    const events = buildCalendarEvents([
      {
        window: window({
          dueDates: [
            { level: UNIT_LEVEL_CODE.Muqam, dueAt: "2026-10-05T12:00:00Z" },
            { level: UNIT_LEVEL_CODE.Dila, dueAt: "2026-10-10T12:00:00Z" },
          ],
        }),
        reportTypeId: "report-type-1",
        reportTypeName: "Monthly",
      },
    ]);

    expect(events.filter((e) => e.type === "due")).toHaveLength(2);
  });

  it("merges events across multiple report types", () => {
    const events = buildCalendarEvents([
      { window: window({ id: "w1" }), reportTypeId: "report-type-1", reportTypeName: "Monthly" },
      { window: window({ id: "w2", reportTypeId: "report-type-2" }), reportTypeId: "report-type-2", reportTypeName: "Quarterly Finance" },
    ]);

    expect(events.filter((e) => e.windowId === "w1")).toHaveLength(3);
    expect(events.filter((e) => e.windowId === "w2")).toHaveLength(3);
  });
});

describe("groupEventsByDate", () => {
  it("groups same-day events from different windows together", () => {
    const events = buildCalendarEvents([
      { window: window({ id: "w1" }), reportTypeId: "report-type-1", reportTypeName: "Monthly" },
      {
        window: window({ id: "w2", dueDates: [{ level: UNIT_LEVEL_CODE.Dila, dueAt: "2026-10-05T12:00:00Z" }] }),
        reportTypeId: "report-type-2",
        reportTypeName: "Quarterly",
      },
    ]);

    const grouped = groupEventsByDate(events);
    expect(grouped.get("2026-10-05")).toHaveLength(2);
  });
});

describe("buildMonthGrid", () => {
  it("starts the grid on the Monday on or before the 1st", () => {
    const days = buildMonthGrid(2026, 9); // October 2026 — 1 Oct is a Thursday
    expect(days[0].getDay()).toBe(1);
    expect(days[0].getDate()).toBe(28);
    expect(days[0].getMonth()).toBe(8);
  });

  it("ends the grid on the Sunday on or after the last day of the month", () => {
    const days = buildMonthGrid(2026, 9);
    const last = days[days.length - 1];
    expect(last.getDay()).toBe(0);
  });

  it("produces full weeks", () => {
    const days = buildMonthGrid(2026, 9);
    expect(days.length % 7).toBe(0);
  });
});
