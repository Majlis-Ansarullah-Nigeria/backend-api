"use client";

import { ArrowRight, History as HistoryIcon } from "lucide-react";
import { useTranslations } from "next-intl";
import { EmptyState } from "@/components/shared/EmptyState";
import { FeatureAwareErrorState } from "@/components/shared/FeatureAwareErrorState";
import { ApiError } from "@/lib/api/client";
import type { ReportRevisionEntry, ReportRevisionHistory } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetReportRevisions } from "@/lib/api/generated/history/history";
import { REPORT_REVISION_ACTION } from "@/features/history/utils/comparison";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";

const ACTION_KEYS: Record<number, "actionSubmitted" | "actionWithdrawn" | "actionResubmitted"> = {
  [REPORT_REVISION_ACTION.Submitted]: "actionSubmitted",
  [REPORT_REVISION_ACTION.Withdrawn]: "actionWithdrawn",
  [REPORT_REVISION_ACTION.Resubmitted]: "actionResubmitted",
};

/** FR-HIS-006: every submission, withdrawal and resubmission of one report, newest first, each
 * with what changed since the entry before it. Used on its own page from History and inline in
 * the Secretariat's review pane. */
export function ReportVersionsPanel({ reportId, headingLevel = 2 }: { reportId: string; headingLevel?: 1 | 2 }) {
  const t = useTranslations("Versions");
  // The page's own heading on its standalone screen; a section heading inside the review pane.
  const Heading = headingLevel === 1 ? "h1" : "h2";
  const query = useGetReportRevisions<ReportRevisionHistory, ApiError>(reportId);

  return (
    <section className="rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby={`versions-${reportId}`}>
      <Heading id={`versions-${reportId}`} className="text-lg font-bold text-ink">
        {t("title")}
      </Heading>
      {query.data ? (
        <p className="mt-1 text-sm text-ink-muted">
          {query.data.unitName} · {query.data.reportTypeName} · {query.data.periodLabel}
        </p>
      ) : null}

      {query.isPending ? (
        <p className="mt-3 text-sm text-ink-muted">{t("loading")}</p>
      ) : query.isError ? (
        <FeatureAwareErrorState error={query.error} title={t("loadErrorTitle")} onRetry={() => query.refetch()} />
      ) : query.data.revisions.length === 0 ? (
        <EmptyState icon={HistoryIcon} title={t("emptyTitle")} description={t("emptyDescription")} className="mt-3" />
      ) : (
        <ol className="mt-4 flex flex-col gap-4">
          {query.data.revisions.map((revision) => (
            <RevisionItem key={Number(revision.revisionNo)} revision={revision} />
          ))}
        </ol>
      )}
    </section>
  );
}

function RevisionItem({ revision }: { revision: ReportRevisionEntry }) {
  const fmt = useOrgFormat();
  const t = useTranslations("Versions");
  const actionKey = ACTION_KEYS[Number(revision.action)] ?? "actionSubmitted";
  const isFirst = Number(revision.revisionNo) === 1;

  return (
    <li className="border-l-2 border-line-strong pl-4">
      <p className="font-semibold text-ink">
        {t("revisionHeading", { number: Number(revision.revisionNo), action: t(actionKey) })}
      </p>
      <p className="text-xs text-ink-muted">
        {revision.actorName
          ? t("revisionMeta", { date: fmt.dateTime(revision.createdAt), actor: revision.actorName })
          : fmt.dateTime(revision.createdAt)}
      </p>

      {isFirst ? null : revision.changes.length === 0 ? (
        <p className="mt-2 text-sm text-ink-muted">{t("noChanges")}</p>
      ) : (
        <div className="mt-2 overflow-x-auto">
          <table className="w-full min-w-[480px] border-collapse text-sm">
            <caption className="sr-only">{t("changesCaption", { number: Number(revision.revisionNo) })}</caption>
            <thead>
              <tr className="border-b border-line text-left text-xs font-bold uppercase tracking-wide text-ink-muted">
                <th scope="col" className="py-2 pr-3">
                  {t("columnField")}
                </th>
                <th scope="col" className="py-2 pr-3">
                  {t("columnBefore")}
                </th>
                <th scope="col" className="py-2">
                  <span className="sr-only">{t("changedTo")}</span>
                </th>
                <th scope="col" className="py-2 pl-3">
                  {t("columnAfter")}
                </th>
              </tr>
            </thead>
            <tbody>
              {revision.changes.map((change) => (
                <tr key={change.fieldKey} className="border-b border-line last:border-0">
                  <th scope="row" className="py-2 pr-3 text-left font-normal text-ink">
                    {change.label}
                    <span className="block text-xs text-ink-muted">{change.sectionTitle}</span>
                  </th>
                  <td className="py-2 pr-3 text-ink-muted line-through decoration-ink-muted/60">{change.before}</td>
                  <td className="py-2 text-ink-muted">
                    <ArrowRight className="size-4" aria-hidden="true" />
                  </td>
                  <td className="py-2 pl-3 font-semibold text-ink">{change.after}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </li>
  );
}
