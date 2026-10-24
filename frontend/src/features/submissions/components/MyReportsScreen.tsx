"use client";

import { FileText } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { StatusChip } from "@/components/shared/StatusChip";
import type { ApiError } from "@/lib/api/client";
import type { MyReportSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetMyReports } from "@/lib/api/generated/submissions/submissions";
import { cn } from "@/lib/utils";
import { countdownText } from "@/features/submissions/utils/countdown";
import { isActionableStatus, isSentStatus, REPORT_LINE_STATUS, reportStatusChipVariant } from "@/features/submissions/utils/report-status";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";

type Tab = "due" | "submitted" | "all";

export function MyReportsScreen() {
  const t = useTranslations("MyReports");
  const reports = useGetMyReports<MyReportSummary[], ApiError>();
  const [tab, setTab] = useState<Tab>("due");

  if (reports.isPending) {
    return <p className="p-4 text-sm text-ink-muted sm:p-6">{t("loading")}</p>;
  }

  if (reports.isError) {
    return (
      <div className="p-4 sm:p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => reports.refetch()} />
      </div>
    );
  }

  const all = reports.data;
  const dueThisWeek = all.filter((r) => isActionableStatus(r.status) && r.status !== REPORT_LINE_STATUS.Upcoming && daysUntil(r.dueAt) <= 7);
  const needsRevision = all.filter((r) => r.status === REPORT_LINE_STATUS.Returned);
  const submitted = all.filter((r) => isSentStatus(r.status));
  const upcoming = all.filter((r) => r.status === REPORT_LINE_STATUS.Upcoming);

  const visible = tab === "due" ? all.filter((r) => isActionableStatus(r.status)) : tab === "submitted" ? submitted : all;
  const sorted = [...visible].sort((a, b) => a.dueAt.localeCompare(b.dueAt));

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
      </div>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <SummaryCard label={t("dueThisWeek")} value={dueThisWeek.length} detail={dueSoonDetail(dueThisWeek, t)} />
        <SummaryCard label={t("needsRevision")} value={needsRevision.length} detail={needsRevision[0]?.reportTypeName} tone="returned" />
        <SummaryCard label={t("submittedCard")} value={submitted.length} detail={t("awaitingReview")} tone="submitted" />
        <SummaryCard label={t("upcomingCard")} value={upcoming.length} detail={upcoming[0]?.reportTypeName} tone="upcoming" />
      </div>

      <div className="flex gap-1" role="tablist" aria-label={t("filterLabel")}>
        <FilterTab active={tab === "due"} onClick={() => setTab("due")} label={t("tabDue")} />
        <FilterTab active={tab === "submitted"} onClick={() => setTab("submitted")} label={t("tabSubmitted")} />
        <FilterTab active={tab === "all"} onClick={() => setTab("all")} label={t("tabAll")} />
      </div>

      {sorted.length === 0 ? (
        <EmptyState icon={FileText} title={t("emptyTitle")} description={t("emptyDescription")} />
      ) : (
        <div className="overflow-x-auto rounded-lg border border-line bg-surface">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-line text-xs uppercase tracking-wide text-ink-muted">
                <th scope="col" className="px-4 py-3 font-medium">
                  {t("columnReport")}
                </th>
                <th scope="col" className="px-4 py-3 font-medium">
                  {t("columnUnit")}
                </th>
                <th scope="col" className="px-4 py-3 font-medium">
                  {t("columnDue")}
                </th>
                <th scope="col" className="px-4 py-3 font-medium">
                  {t("columnStatus")}
                </th>
                <th scope="col" className="px-4 py-3 font-medium">
                  <span className="sr-only">{t("columnAction")}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {sorted.map((report) => (
                <ReportRow key={`${report.windowId}-${report.unitId}`} report={report} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function ReportRow({ report }: { report: MyReportSummary }) {
  const fmt = useOrgFormat();
  const t = useTranslations("MyReports");
  const href = `/reports/${report.instanceId ?? report.windowId}/edit?unit=${report.unitId}${report.status === REPORT_LINE_STATUS.Returned ? "&returned=1" : ""}`;
  const actionLabel =
    report.status === REPORT_LINE_STATUS.Upcoming
      ? t("actionPreview")
      : report.status === REPORT_LINE_STATUS.NotStarted
        ? t("actionStart")
        : report.status === REPORT_LINE_STATUS.Draft || report.status === REPORT_LINE_STATUS.Overdue
          ? t("actionContinue")
          : report.status === REPORT_LINE_STATUS.Returned
            ? t("actionRevise")
            : t("actionView");

  return (
    <tr className="border-b border-line last:border-0">
      <td className="px-4 py-3">
        <p className="font-medium text-ink">{report.reportTypeName}</p>
        <p className="text-xs text-ink-muted">{report.periodLabel}</p>
      </td>
      <td className="px-4 py-3 text-ink-muted">{report.unitName}</td>
      <td className="px-4 py-3 text-ink-muted">
        {fmt.date(report.dueAt)}
        <span className="block text-xs">{countdownText(report.dueAt)}</span>
      </td>
      <td className="px-4 py-3">
        <StatusChip status={reportStatusChipVariant(report.status)} />
      </td>
      <td className="px-4 py-3 text-right">
        {report.status === REPORT_LINE_STATUS.Missed || report.status === REPORT_LINE_STATUS.Acknowledged ? (
          <Link href={href} className="text-sm font-medium text-ink-muted underline-offset-2 hover:underline">
            {t("actionView")}
          </Link>
        ) : (
          <Link href={href} className="text-sm font-medium text-green-700 underline-offset-2 hover:underline">
            {actionLabel}
          </Link>
        )}
      </td>
    </tr>
  );
}

function SummaryCard({
  label,
  value,
  detail,
  tone,
}: {
  label: string;
  value: number;
  detail?: string;
  tone?: "returned" | "submitted" | "upcoming";
}) {
  const toneClass =
    tone === "returned"
      ? "text-status-returned-fg"
      : tone === "submitted"
        ? "text-status-submitted-fg"
        : tone === "upcoming"
          ? "text-status-upcoming-fg"
          : "text-ink";

  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{label}</p>
      <p className={cn("mt-1 text-3xl font-bold", toneClass)}>{value}</p>
      {detail ? <p className="mt-1 truncate text-xs text-ink-muted">{detail}</p> : null}
    </div>
  );
}

function FilterTab({ active, onClick, label }: { active: boolean; onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={cn(
        "min-h-9 rounded-md px-3 text-sm font-medium transition-colors",
        active ? "bg-mint text-green-700" : "text-ink-muted hover:bg-ground",
      )}
    >
      {label}
    </button>
  );
}

function daysUntil(iso: string): number {
  return Math.round((new Date(iso).getTime() - Date.now()) / (24 * 60 * 60 * 1000));
}

function dueSoonDetail(items: MyReportSummary[], t: ReturnType<typeof useTranslations>): string | undefined {
  const dueTomorrow = items.filter((item) => daysUntil(item.dueAt) <= 1).length;
  return dueTomorrow > 0 ? t("dueTomorrowCount", { count: dueTomorrow }) : undefined;
}
