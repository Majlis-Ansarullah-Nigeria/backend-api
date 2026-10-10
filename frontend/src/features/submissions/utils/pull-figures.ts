import { AGGREGATION_RULE } from "@/lib/form-engine/field-types";
import { REPORT_LINE_STATUS } from "@/features/submissions/utils/report-status";

// Short tags for the "Pulled · {rule}" badge (FR-SUB-007) — field-type-meta.ts's
// AGGREGATION_LABELS are the longer builder-facing descriptions ("Sum up the hierarchy"), too
// verbose for an inline field tag.
const SHORT_AGGREGATION_LABELS: Record<number, string> = {
  [AGGREGATION_RULE.Sum]: "sum",
  [AGGREGATION_RULE.Average]: "average",
  [AGGREGATION_RULE.Min]: "lowest",
  [AGGREGATION_RULE.Max]: "highest",
  [AGGREGATION_RULE.Latest]: "latest",
};

export function shortAggregationLabel(rule: number): string {
  return SHORT_AGGREGATION_LABELS[rule] ?? "";
}

// Matches PullChildFiguresCommandHandler's SubmittedStatuses exactly (backend/src/HRS.Application/
// Features/Submissions/PullFigures/PullChildFiguresCommandHandler.cs) — a Returned child still has
// real submitted data to pull, unlike MyReportsScreen's "Sent" tab which deliberately excludes it.
const PULL_ELIGIBLE_STATUSES: ReadonlySet<number> = new Set([
  REPORT_LINE_STATUS.Submitted,
  REPORT_LINE_STATUS.SubmittedLate,
  REPORT_LINE_STATUS.InReview,
  REPORT_LINE_STATUS.Returned,
  REPORT_LINE_STATUS.Acknowledged,
]);

export function isPullEligibleStatus(status: number): boolean {
  return PULL_ELIGIBLE_STATUSES.has(status);
}
