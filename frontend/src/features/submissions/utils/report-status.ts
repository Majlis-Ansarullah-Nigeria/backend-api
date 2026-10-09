import type { StatusChipVariant } from "@/components/shared/StatusChip";

// Matches backend/src/HRS.Domain/Submissions/ReportInstanceStatus.cs's enum order exactly — the
// API serializes it as a plain number. This is the authoritative, persisted status (section 5 of
// the SRS); ReportLineStatus below is the richer, derived display status M5.2's list computes.
export const REPORT_INSTANCE_STATUS = {
  NotStarted: 0,
  Draft: 1,
  Submitted: 2,
  InReview: 3,
  Returned: 4,
  Acknowledged: 5,
  Missed: 6,
} as const;

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

// A direct passthrough from the authoritative ReportInstanceStatus to the richer ReportLineStatus
// — the single-report editing screen doesn't need M5.2's Upcoming/Overdue window-phase nuance
// (it already shows the due countdown and a ReadOnlyBanner directly), just the right chip label.
const INSTANCE_TO_LINE_STATUS: Record<number, number> = {
  [REPORT_INSTANCE_STATUS.NotStarted]: REPORT_LINE_STATUS.NotStarted,
  [REPORT_INSTANCE_STATUS.Draft]: REPORT_LINE_STATUS.Draft,
  [REPORT_INSTANCE_STATUS.Submitted]: REPORT_LINE_STATUS.Submitted,
  [REPORT_INSTANCE_STATUS.InReview]: REPORT_LINE_STATUS.InReview,
  [REPORT_INSTANCE_STATUS.Returned]: REPORT_LINE_STATUS.Returned,
  [REPORT_INSTANCE_STATUS.Acknowledged]: REPORT_LINE_STATUS.Acknowledged,
  [REPORT_INSTANCE_STATUS.Missed]: REPORT_LINE_STATUS.Missed,
};

export function reportInstanceStatusChipVariant(status: number): StatusChipVariant {
  return reportStatusChipVariant(INSTANCE_TO_LINE_STATUS[status] ?? REPORT_LINE_STATUS.NotStarted);
}

export function canSubmitFromStatus(status: number): boolean {
  return status === REPORT_INSTANCE_STATUS.Draft || status === REPORT_INSTANCE_STATUS.Returned;
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
