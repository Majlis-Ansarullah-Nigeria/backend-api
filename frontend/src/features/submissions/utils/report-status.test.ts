import { describe, expect, it } from "vitest";
import { isActionableStatus, isSentStatus, REPORT_LINE_STATUS, reportStatusChipVariant } from "./report-status";

describe("reportStatusChipVariant", () => {
  it("maps every known status to its chip variant", () => {
    expect(reportStatusChipVariant(REPORT_LINE_STATUS.Upcoming)).toBe("upcoming");
    expect(reportStatusChipVariant(REPORT_LINE_STATUS.NotStarted)).toBe("not-started");
    expect(reportStatusChipVariant(REPORT_LINE_STATUS.Draft)).toBe("draft");
    expect(reportStatusChipVariant(REPORT_LINE_STATUS.Overdue)).toBe("overdue");
    expect(reportStatusChipVariant(REPORT_LINE_STATUS.Submitted)).toBe("submitted");
    expect(reportStatusChipVariant(REPORT_LINE_STATUS.SubmittedLate)).toBe("late");
    expect(reportStatusChipVariant(REPORT_LINE_STATUS.InReview)).toBe("in-review");
    expect(reportStatusChipVariant(REPORT_LINE_STATUS.Returned)).toBe("returned");
    expect(reportStatusChipVariant(REPORT_LINE_STATUS.Acknowledged)).toBe("acknowledged");
    expect(reportStatusChipVariant(REPORT_LINE_STATUS.Missed)).toBe("missed");
  });

  it("falls back to not-started for an unknown status", () => {
    expect(reportStatusChipVariant(99)).toBe("not-started");
  });
});

describe("isActionableStatus", () => {
  it("is true for statuses the head can still act on", () => {
    expect(isActionableStatus(REPORT_LINE_STATUS.Upcoming)).toBe(true);
    expect(isActionableStatus(REPORT_LINE_STATUS.NotStarted)).toBe(true);
    expect(isActionableStatus(REPORT_LINE_STATUS.Draft)).toBe(true);
    expect(isActionableStatus(REPORT_LINE_STATUS.Overdue)).toBe(true);
    expect(isActionableStatus(REPORT_LINE_STATUS.Returned)).toBe(true);
  });

  it("is false for statuses already out of the head's hands", () => {
    expect(isActionableStatus(REPORT_LINE_STATUS.Submitted)).toBe(false);
    expect(isActionableStatus(REPORT_LINE_STATUS.InReview)).toBe(false);
    expect(isActionableStatus(REPORT_LINE_STATUS.Acknowledged)).toBe(false);
    expect(isActionableStatus(REPORT_LINE_STATUS.Missed)).toBe(false);
  });
});

describe("isSentStatus", () => {
  it("is true for submitted/in-review/acknowledged reports", () => {
    expect(isSentStatus(REPORT_LINE_STATUS.Submitted)).toBe(true);
    expect(isSentStatus(REPORT_LINE_STATUS.SubmittedLate)).toBe(true);
    expect(isSentStatus(REPORT_LINE_STATUS.InReview)).toBe(true);
    expect(isSentStatus(REPORT_LINE_STATUS.Acknowledged)).toBe(true);
  });

  it("is false for reports still pending action", () => {
    expect(isSentStatus(REPORT_LINE_STATUS.Draft)).toBe(false);
    expect(isSentStatus(REPORT_LINE_STATUS.Returned)).toBe(false);
  });
});
