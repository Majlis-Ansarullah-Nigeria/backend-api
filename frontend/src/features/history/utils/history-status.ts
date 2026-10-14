import type { useTranslations } from "next-intl";
import { REPORT_LINE_STATUS } from "@/features/submissions/utils/report-status";

/** The six "past report" statuses (FR-HIS-001) — matches backend's HistoryStatuses.All exactly,
 * in the same order as the mockup's Status options. */
export const HISTORY_STATUS_ORDER: number[] = [
  REPORT_LINE_STATUS.Submitted,
  REPORT_LINE_STATUS.SubmittedLate,
  REPORT_LINE_STATUS.InReview,
  REPORT_LINE_STATUS.Returned,
  REPORT_LINE_STATUS.Acknowledged,
  REPORT_LINE_STATUS.Missed,
];

export function historyStatusOptionLabel(t: ReturnType<typeof useTranslations>, status: number): string {
  switch (status) {
    case REPORT_LINE_STATUS.Submitted:
      return t("statusSubmitted");
    case REPORT_LINE_STATUS.SubmittedLate:
      return t("statusSubmittedLate");
    case REPORT_LINE_STATUS.InReview:
      return t("statusInReview");
    case REPORT_LINE_STATUS.Returned:
      return t("statusReturned");
    case REPORT_LINE_STATUS.Acknowledged:
      return t("statusAcknowledged");
    case REPORT_LINE_STATUS.Missed:
      return t("statusMissed");
    default:
      return "";
  }
}
