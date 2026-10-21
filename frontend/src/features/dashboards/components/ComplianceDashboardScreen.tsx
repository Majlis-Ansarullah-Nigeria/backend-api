"use client";

import { Bell, ChevronRight, LineChart } from "lucide-react";
import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api/client";
import type { ComplianceDashboard, ComplianceGroupRow, SendRemindersResult } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetComplianceDashboard, useSendComplianceReminders } from "@/lib/api/generated/dashboards/dashboards";
import { OnTimeLateChart } from "@/features/dashboards/components/OnTimeLateChart";
import { complianceKpis, isOnTrack, percentOf, type PeriodCounts, UNIT_LEVEL } from "@/features/dashboards/utils/compliance";
import { cn } from "@/lib/utils";

const DATE_FORMATTER = new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", timeZone: "Africa/Lagos" });

const SELECT_CLASS =
  "h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40";

export interface ComplianceSelection {
  reportTypeId?: string;
  level?: number;
}

/** FR-DSH-001: the "Compliance dashboard" board. */
export function ComplianceDashboardScreen({ initial }: { initial: ComplianceSelection }) {
  const t = useTranslations("Compliance");
  const router = useRouter();
  const [selection, setSelection] = useState<ComplianceSelection>(initial);
  const [remindStatus, setRemindStatus] = useState<{ group: string; result?: SendRemindersResult; error?: string } | null>(null);

  const query = useGetComplianceDashboard<ComplianceDashboard, ApiError>({ reportTypeId: selection.reportTypeId, level: selection.level });
  const remind = useSendComplianceReminders();

  const select = (patch: ComplianceSelection) => {
    const next = { ...selection, ...patch };
    setSelection(next);
    setRemindStatus(null);
    const params = new URLSearchParams();
    if (next.reportTypeId) {
      params.set("reportTypeId", next.reportTypeId);
    }
    if (next.level != null) {
      params.set("level", String(next.level));
    }
    router.replace(`/secretariat/dashboards/compliance${params.size > 0 ? `?${params.toString()}` : ""}`);
  };

  if (query.isPending) {
    return <div className="p-4 text-sm text-ink-muted sm:p-6">{t("loading")}</div>;
  }

  if (query.isError) {
    return (
      <div className="p-4 sm:p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => query.refetch()} />
      </div>
    );
  }

  const data = query.data;
  const level = data.levels.find((option) => Number(option.code) === Number(data.level));
  const periods: PeriodCounts[] = data.periods.map((period) => ({
    periodLabel: period.periodLabel,
    isClosed: period.isClosed,
    expected: Number(period.expected),
    onTime: Number(period.onTime),
    late: Number(period.late),
    missed: Number(period.missed),
    drafting: Number(period.drafting),
    notStarted: Number(period.notStarted),
  }));
  const kpis = complianceKpis(periods);
  const latest = data.periods.at(-1);

  const sendReminder = (group: ComplianceGroupRow) => {
    if (!latest || data.level == null) {
      return;
    }
    setRemindStatus(null);
    remind.mutate(
      { data: { windowId: latest.windowId, level: data.level, groupUnitId: group.groupUnitId } },
      {
        onSuccess: (result) => setRemindStatus({ group: group.name, result }),
        onError: (error) => setRemindStatus({ group: group.name, error: error instanceof ApiError ? error.message : t("remindFailed") }),
      },
    );
  };

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <nav aria-label={t("breadcrumbLabel")} className="flex items-center gap-2 text-sm">
        <span className="text-ink-muted">{t("breadcrumbDashboards")}</span>
        <ChevronRight className="size-4 text-ink-muted" aria-hidden="true" />
        <span className="font-semibold text-ink" aria-current="page">
          {t("breadcrumbCompliance")}
        </span>
      </nav>

      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-ink sm:text-3xl">{t("title")}</h1>
          {data.reportTypeName ? (
            <p className="mt-1 text-sm text-ink-muted">{t("subtitle", { reportType: data.reportTypeName, count: periods.length })}</p>
          ) : null}
        </div>

        <div className="flex flex-wrap items-end gap-3">
          {data.reportTypes.length > 1 ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="compliance-report-type">{t("reportTypeLabel")}</Label>
              <select
                id="compliance-report-type"
                value={data.reportTypeId ?? ""}
                onChange={(event) => select({ reportTypeId: event.target.value, level: undefined })}
                className={SELECT_CLASS}
              >
                {data.reportTypes.map((reportType) => (
                  <option key={reportType.id} value={reportType.id}>
                    {reportType.name}
                  </option>
                ))}
              </select>
            </div>
          ) : null}

          {data.levels.length > 0 ? (
            <div role="group" aria-label={t("levelLabel")} className="flex rounded-md bg-mint/60 p-1">
              {data.levels.map((option) => {
                const active = Number(option.code) === Number(data.level);
                return (
                  <button
                    key={Number(option.code)}
                    type="button"
                    aria-pressed={active}
                    onClick={() => select({ level: Number(option.code) })}
                    className={cn(
                      "h-10 rounded-sm px-4 text-sm font-semibold",
                      active ? "bg-surface text-ink shadow-sm" : "text-ink-muted hover:text-ink",
                    )}
                  >
                    {option.name}
                  </button>
                );
              })}
            </div>
          ) : null}
        </div>
      </div>

      {!data.reportTypeId || periods.length === 0 ? (
        <EmptyState icon={LineChart} title={t("emptyTitle")} description={t("emptyDescription")} />
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <KpiTile
              label={kpis.lastFull ? t("kpiOnTimeLabel") : t("kpiOnTimeLabelNone")}
              value={kpis.onTimePercent}
              tone="text-green-700"
              detail={
                kpis.lastFull
                  ? kpis.onTimeChange == null
                    ? kpis.lastFull.periodLabel
                    : t(kpis.onTimeChange > 0 ? "kpiOnTimeUp" : kpis.onTimeChange < 0 ? "kpiOnTimeDown" : "kpiOnTimeSame", {
                        period: kpis.lastFull.periodLabel,
                        points: Math.abs(kpis.onTimeChange),
                      })
                  : undefined
              }
            />
            <KpiTile label={t("kpiAverageLabel", { count: periods.filter((p) => p.isClosed).length })} value={kpis.averageOnTimePercent} detail={t("kpiAverageDetail")} />
            <KpiTile
              label={kpis.lastFull ? t("kpiLateLabel", { period: kpis.lastFull.periodLabel }) : t("kpiLateLabelNone")}
              value={kpis.latePercent}
              tone="text-status-late-fg"
              detail={kpis.lastFull && level ? `${kpis.lastFull.late} ${level.pluralName}` : undefined}
            />
            <KpiTile
              label={kpis.lastFull ? t("kpiMissedLabel", { period: kpis.lastFull.periodLabel }) : t("kpiMissedLabelNone")}
              value={kpis.missedPercent}
              tone="text-danger"
              detail={kpis.lastFull && level ? `${kpis.lastFull.missed} ${level.pluralName}` : undefined}
            />
          </div>

          <section className="rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="compliance-chart-title">
            <h2 id="compliance-chart-title" className="text-lg font-bold text-ink">
              {t("chartTitle")}
            </h2>
            <OnTimeLateChart
              labels={{
                onTime: t("legendOnTime"),
                late: t("legendLate"),
                open: t("openLabel"),
                submittedSoFar: t("submittedSoFar"),
                ariaLabel: t("chartAriaLabel"),
              }}
              points={periods.map((period) => ({
                periodLabel: period.periodLabel,
                isOpen: !period.isClosed,
                onTime: percentOf(period.isClosed ? period.onTime : period.onTime + period.late, period.expected),
                late: period.isClosed ? percentOf(period.late, period.expected) : 0,
              }))}
            />
          </section>

          {latest ? (
            <section aria-labelledby="compliance-groups-title" className="flex flex-col gap-3">
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <h2 id="compliance-groups-title" className="text-lg font-bold text-ink">
                  {latest.isClosed
                    ? t("groupsTitleClosed", { period: latest.periodLabel })
                    : t("groupsTitleOpen", { period: latest.periodLabel })}
                </h2>
                {latest.dueAt && level ? (
                  <p className="text-sm text-ink-muted">
                    {t(new Date(latest.dueAt) < new Date() ? "dueWas" : "dueIs", {
                      level: level.name,
                      date: DATE_FORMATTER.format(new Date(latest.dueAt)),
                    })}
                  </p>
                ) : null}
              </div>

              {remindStatus ? (
                <p role="status" className={cn("text-sm", remindStatus.error ? "text-danger" : "text-green-700")}>
                  {remindStatus.error ??
                    t("remindResult", {
                      group: remindStatus.group,
                      reminded: Number(remindStatus.result?.unitsReminded ?? 0),
                      withoutHead: Number(remindStatus.result?.unitsWithoutHead ?? 0),
                    })}
                </p>
              ) : null}

              <div className="overflow-x-auto rounded-lg border border-line bg-surface">
                <table className="w-full min-w-[720px] border-collapse text-sm">
                  <thead>
                    <tr className="border-b border-line text-xs font-bold uppercase tracking-wide text-ink-muted">
                      <th scope="col" className="px-4 py-3 text-left">
                        {Number(data.level) === UNIT_LEVEL.Department ? t("columnDepartment") : t("columnZone")}
                      </th>
                      <th scope="col" className="px-4 py-3 text-left">
                        {t("columnLevelSubmitted", { level: level?.name ?? "" })}
                      </th>
                      <th scope="col" className="px-4 py-3 text-right">
                        {t("columnSubmitted")}
                      </th>
                      <th scope="col" className="px-4 py-3 text-right">
                        {t("columnDrafting")}
                      </th>
                      <th scope="col" className="px-4 py-3 text-right">
                        {t("columnNotStarted")}
                      </th>
                      <th scope="col" className="px-4 py-3">
                        <span className="sr-only">{t("columnActions")}</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.groups.map((group) => {
                      const expected = Number(group.expected);
                      const submitted = Number(group.submitted);
                      const percent = percentOf(submitted, expected);
                      const remindable = Number(group.remindable);
                      return (
                        <tr key={group.groupUnitId ?? group.name} className="border-b border-line last:border-0">
                          <th scope="row" className="px-4 py-3.5 text-left font-semibold text-ink">
                            {group.name}
                          </th>
                          <td className="px-4 py-3.5">
                            <div className="flex items-center gap-3">
                              <div className="h-2 w-48 max-w-full rounded-full bg-line" aria-hidden="true">
                                <div
                                  className={cn("h-2 rounded-full", isOnTrack(percent) ? "bg-chart-on-time" : "bg-chart-late")}
                                  style={{ width: `${percent}%` }}
                                />
                              </div>
                              <span className="font-bold tabular-nums text-ink">{percent}%</span>
                            </div>
                          </td>
                          <td className="px-4 py-3.5 text-right tabular-nums text-ink">
                            {submitted} / {expected}
                          </td>
                          <td className="px-4 py-3.5 text-right tabular-nums text-ink">{Number(group.drafting)}</td>
                          <td className="px-4 py-3.5 text-right tabular-nums text-ink">{Number(group.notStarted)}</td>
                          <td className="px-4 py-3.5 text-right">
                            {data.canRemind ? (
                              <Button
                                type="button"
                                variant="outline"
                                size="sm"
                                disabled={remindable === 0 || remind.isPending}
                                onClick={() => sendReminder(group)}
                                aria-label={t("remindLabel", { group: group.name, count: remindable })}
                              >
                                <Bell aria-hidden="true" />
                                {t("remindAction")}
                              </Button>
                            ) : null}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </section>
          ) : null}
        </>
      )}
    </div>
  );
}

function KpiTile({ label, value, detail, tone = "text-ink" }: { label: string; value: number | null; detail?: string; tone?: string }) {
  return (
    <div className="rounded-lg border border-line bg-surface p-5">
      <p className="text-xs font-bold uppercase tracking-wide text-ink-muted">{label}</p>
      <p className={cn("mt-2 text-4xl font-bold tabular-nums", tone)}>{value == null ? "—" : `${value}%`}</p>
      {detail ? <p className="mt-1 text-sm text-ink-muted">{detail}</p> : null}
    </div>
  );
}
