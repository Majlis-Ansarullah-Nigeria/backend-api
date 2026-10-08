"use client";

import { Eye } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ErrorState } from "@/components/shared/ErrorState";
import { StatusChip } from "@/components/shared/StatusChip";
import type { ApiError } from "@/lib/api/client";
import type { MeProfile, MyReportSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetMe } from "@/lib/api/generated/identity/identity";
import { useGetMyReports } from "@/lib/api/generated/submissions/submissions";
import { countdownText } from "@/features/submissions/utils/countdown";
import { isActionableStatus, isSentStatus, REPORT_LINE_STATUS, reportStatusChipVariant } from "@/features/submissions/utils/report-status";

const DATE_FORMATTER = new Intl.DateTimeFormat("en", { weekday: "long", day: "numeric", month: "long", year: "numeric" });

export function HeadHomeScreen() {
  const t = useTranslations("HeadHome");
  const me = useGetMe<MeProfile, ApiError>();
  const reports = useGetMyReports<MyReportSummary[], ApiError>();

  if (me.isPending || reports.isPending) {
    return <p className="p-4 text-sm text-ink-muted sm:p-6">{t("loading")}</p>;
  }

  if (me.isError || reports.isError) {
    return (
      <div className="p-4 sm:p-6">
        <ErrorState
          title={t("loadErrorTitle")}
          onRetry={() => {
            me.refetch();
            reports.refetch();
          }}
        />
      </div>
    );
  }

  const firstName = me.data.name.split(" ")[0];
  const actionable = reports.data
    .filter((r) => isActionableStatus(r.status) && r.status !== REPORT_LINE_STATUS.Upcoming)
    .sort((a, b) => a.dueAt.localeCompare(b.dueAt));
  const dueThisWeek = actionable.filter((r) => daysUntil(r.dueAt) <= 7).length;
  const [banner, ...rest] = actionable;
  const dueSoonCards = rest.slice(0, 2);

  const recent = reports.data
    .filter((r) => isSentStatus(r.status) && r.submittedAt)
    .sort((a, b) => (b.submittedAt ?? "").localeCompare(a.submittedAt ?? ""))
    .slice(0, 4);

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-ink">{t("greeting", { name: firstName })}</h1>
          <p className="mt-1 text-sm text-ink-muted">
            {DATE_FORMATTER.format(new Date())} · {t("dueThisWeekCount", { count: dueThisWeek })}
          </p>
        </div>
        <Link href="/reports">
          <Button type="button" variant="outline">
            {t("viewAllReports")}
          </Button>
        </Link>
      </div>

      {banner ? (
        <div className="flex flex-col gap-3 rounded-lg bg-forest-900 p-6 text-white sm:flex-row sm:items-center sm:justify-between">
          <div>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-lime/20 px-2.5 py-1 text-sm font-medium text-lime">
              {countdownText(banner.dueAt)}
            </span>
            <h2 className="mt-2 text-xl font-bold">
              {banner.reportTypeName} · {banner.periodLabel}
            </h2>
          </div>
          <Link href={`/reports/${banner.instanceId ?? banner.windowId}/edit?unit=${banner.unitId}`}>
            <Button type="button" variant="primary">
              {banner.status === REPORT_LINE_STATUS.Draft || banner.status === REPORT_LINE_STATUS.Overdue
                ? t("continueReport")
                : banner.status === REPORT_LINE_STATUS.Returned
                  ? t("reviseReport")
                  : t("startReport")}
            </Button>
          </Link>
        </div>
      ) : null}

      {dueSoonCards.length > 0 ? (
        <div className="grid gap-4 sm:grid-cols-2">
          {dueSoonCards.map((report) => (
            <div key={`${report.windowId}-${report.unitId}`} className="rounded-lg border border-line bg-surface p-4">
              <StatusChip status={reportStatusChipVariant(report.status)} />
              <h3 className="mt-2 font-bold text-ink">{report.reportTypeName}</h3>
              <p className="text-xs text-ink-muted">
                {report.periodLabel} · {report.unitName}
              </p>
              <p className="mt-1 text-xs text-ink-muted">{countdownText(report.dueAt)}</p>
              <Link
                href={`/reports/${report.instanceId ?? report.windowId}/edit?unit=${report.unitId}`}
                className="mt-3 inline-block text-sm font-medium text-green-700 underline-offset-2 hover:underline"
              >
                {report.status === REPORT_LINE_STATUS.NotStarted ? t("startReport") : t("continueReport")}
              </Link>
            </div>
          ))}
        </div>
      ) : null}

      <div className="rounded-lg border border-line bg-surface p-4">
        <div className="flex items-center justify-between">
          <h2 className="font-bold text-ink">{t("recentReportsTitle")}</h2>
          <Link href="/history" className="text-sm font-medium text-green-700 underline-offset-2 hover:underline">
            {t("seeHistory")}
          </Link>
        </div>
        {recent.length === 0 ? (
          <p className="mt-3 text-sm text-ink-muted">{t("noRecentReports")}</p>
        ) : (
          <div className="mt-3 flex flex-col divide-y divide-line">
            {recent.map((report) => (
              <div key={`${report.windowId}-${report.unitId}`} className="flex items-center justify-between gap-3 py-3">
                <div className="flex items-center gap-3">
                  <Eye className="size-4 shrink-0 text-ink-muted" aria-hidden="true" />
                  <div>
                    <p className="text-sm font-medium text-ink">
                      {report.reportTypeName} · {report.periodLabel}
                    </p>
                    <p className="text-xs text-ink-muted">
                      {t("submittedOn", { date: report.submittedAt ? new Date(report.submittedAt).toLocaleDateString() : "" })}
                    </p>
                  </div>
                </div>
                <StatusChip status={reportStatusChipVariant(report.status)} />
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function daysUntil(iso: string): number {
  return Math.round((new Date(iso).getTime() - Date.now()) / (24 * 60 * 60 * 1000));
}
