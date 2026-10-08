import type { StatusChipVariant } from "@/components/shared/StatusChip";

// Matches backend/src/HRS.Application/Features/Submissions/MyReports/ReportLineStatus.cs's enum
// order exactly — the API serializes it as a plain number. Mirrors StatusChip's ten variants 1:1.
export const REPORT_LINE_STATUS = {
  Upcoming: 0,
  NotStarted: 1,
  Draft: 2,
  Overdue: 3,
  Submitted: 4,
  SubmittedLate: 5,
  InReview: 6,
  Returned: 7,
  Acknowledged: 8,
  Missed: 9,
} as const;

const STATUS_CHIP_VARIANTS: Record<number, StatusChipVariant> = {
  [REPORT_LINE_STATUS.Upcoming]: "upcoming",
  [REPORT_LINE_STATUS.NotStarted]: "not-started",
  [REPORT_LINE_STATUS.Draft]: "draft",
  [REPORT_LINE_STATUS.Overdue]: "overdue",
  [REPORT_LINE_STATUS.Submitted]: "submitted",
  [REPORT_LINE_STATUS.SubmittedLate]: "late",
  [REPORT_LINE_STATUS.InReview]: "in-review",
  [REPORT_LINE_STATUS.Returned]: "returned",
  [REPORT_LINE_STATUS.Acknowledged]: "acknowledged",
  [REPORT_LINE_STATUS.Missed]: "missed",
};

export function reportStatusChipVariant(status: number): StatusChipVariant {
  return STATUS_CHIP_VARIANTS[status] ?? "not-started";
}

/** Statuses the head can still act on — the "Due" tab and Home's due-soon cards. */
const ACTIONABLE_STATUSES: ReadonlySet<number> = new Set([
  REPORT_LINE_STATUS.Upcoming,
  REPORT_LINE_STATUS.NotStarted,
  REPORT_LINE_STATUS.Draft,
  REPORT_LINE_STATUS.Overdue,
  REPORT_LINE_STATUS.Returned,
]);

export function isActionableStatus(status: number): boolean {
  return ACTIONABLE_STATUSES.has(status);
}

/** Statuses that have already left the head's hands — the "Submitted" tab. */
const SENT_STATUSES: ReadonlySet<number> = new Set([
  REPORT_LINE_STATUS.Submitted,
  REPORT_LINE_STATUS.SubmittedLate,
  REPORT_LINE_STATUS.InReview,
  REPORT_LINE_STATUS.Acknowledged,
]);

export function isSentStatus(status: number): boolean {
  return SENT_STATUSES.has(status);
}
