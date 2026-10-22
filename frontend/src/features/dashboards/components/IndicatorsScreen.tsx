"use client";

import { ChevronRight, LineChart } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api/client";
import type { IndicatorDashboard, IndicatorField } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetIndicatorDashboard } from "@/lib/api/generated/dashboards/dashboards";
import { DashboardSwitcher } from "@/features/dashboards/components/DashboardSwitcher";
import { IndicatorChart } from "@/features/dashboards/components/IndicatorChart";
import { MapCard } from "@/features/dashboards/components/MapCard";
import {
  change,
  changeKind,
  formatIndicator,
  formatSigned,
  latestTwo,
  percentOfTarget,
  TARGET_ON_TRACK_PERCENT,
} from "@/features/dashboards/utils/indicators";
import { cn } from "@/lib/utils";

const SELECT_CLASS =
  "h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40";

const PERIOD_OPTIONS = [6, 12, 24] as const;
const KPI_COUNT = 4;

export interface IndicatorSelection {
  reportTypeId?: string;
  fieldKey?: string;
  unitId?: string;
  periods?: number;
}

const toNumber = (value: number | string | null | undefined): number | null => (value == null ? null : Number(value));

/** FR-DSH-002/003/005: the "Indicators, drill-down and map" board (the map is M10.3). basePath
 * is where this instance lives — the Secretariat's dashboards or a head's/Leadership's /dashboards. */
export function IndicatorsScreen({ basePath, initial }: { basePath: string; initial: IndicatorSelection }) {
  const t = useTranslations("Indicators");
  const router = useRouter();
  const [selection, setSelection] = useState<IndicatorSelection>(initial);
  const [mode, setMode] = useState<"line" | "bars">("line");

  const query = useGetIndicatorDashboard<IndicatorDashboard, ApiError>({
    reportTypeId: selection.reportTypeId,
    fieldKey: selection.fieldKey,
    unitId: selection.unitId,
    periods: selection.periods,
  });

  const hrefFor = (patch: IndicatorSelection) => {
    const next = { ...selection, ...patch };
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(next)) {
      if (value != null && value !== "") {
        params.set(key, String(value));
      }
    }
    return params.size > 0 ? `${basePath}?${params.toString()}` : basePath;
  };

  const select = (patch: IndicatorSelection) => {
    setSelection((current) => ({ ...current, ...patch }));
    router.replace(hrefFor(patch));
  };

  if (query.isPending) {
    return <div className="p-4 text-sm text-ink-muted sm:p-6">{t("loading")}</div>;
  }

  if (query.isError) {
    return (
      <div className="p-4 sm:p-6">
        <ErrorState title={query.error.status === 403 ? t("forbiddenTitle") : t("loadErrorTitle")} onRetry={() => query.refetch()} />
      </div>
    );
  }

  const data = query.data;
  const node = data.node;
  const periods = data.periods;
  const seriesByKey = new Map(
    data.series.map((series) => [series.fieldKey, { values: series.values.map(toNumber), target: toNumber(series.target) }]),
  );
  const field = data.fields.find((f) => f.fieldKey === data.fieldKey);
  const fieldSeries = field ? seriesByKey.get(field.fieldKey) : undefined;
  const format = (f: IndicatorField) => (value: number | null) => formatIndicator(value, Number(f.type), f.unitLabel);

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      {basePath.startsWith("/secretariat") ? <DashboardSwitcher current="indicators" /> : null}
      <nav aria-label={t("breadcrumbLabel")} className="flex flex-wrap items-center gap-2 text-sm">
        {data.breadcrumb.map((crumb, index) => {
          const isLast = index === data.breadcrumb.length - 1;
          return (
            <span key={crumb.unitId} className="flex items-center gap-2">
              {isLast ? (
                <span className="font-semibold text-ink" aria-current="page">
                  {crumb.name}
                </span>
              ) : (
                <button
                  type="button"
                  onClick={() => select({ unitId: crumb.unitId })}
                  className="font-medium text-green-700 underline underline-offset-2"
                >
                  {crumb.name}
                </button>
              )}
              {!isLast || data.children.length > 0 ? <ChevronRight className="size-4 text-ink-muted" aria-hidden="true" /> : null}
            </span>
          );
        })}
        {data.children.length > 0 && data.childLevelPluralName ? (
          <span className="text-ink-muted">{t("chooseChild", { level: data.childLevelPluralName })}</span>
        ) : null}
      </nav>

      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-ink sm:text-3xl">{node ? t("title", { unit: node.name }) : t("titleNoUnit")}</h1>
          {data.reportTypeName ? <p className="mt-1 text-sm text-ink-muted">{t("subtitle", { reportType: data.reportTypeName })}</p> : null}
        </div>
        <div className="flex flex-wrap items-end gap-3">
          {data.reportTypes.length > 1 ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="indicators-report-type">{t("reportTypeLabel")}</Label>
              <select
                id="indicators-report-type"
                value={data.reportTypeId ?? ""}
                onChange={(event) => select({ reportTypeId: event.target.value, fieldKey: undefined })}
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
          {data.fields.length > 0 ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="indicators-field">{t("fieldLabel")}</Label>
              <select id="indicators-field" value={data.fieldKey ?? ""} onChange={(event) => select({ fieldKey: event.target.value })} className={SELECT_CLASS}>
                {data.fields.map((f) => (
                  <option key={f.fieldKey} value={f.fieldKey}>
                    {f.label}
                  </option>
                ))}
              </select>
            </div>
          ) : null}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="indicators-periods">{t("periodsLabel")}</Label>
            <select
              id="indicators-periods"
              value={selection.periods ?? 12}
              onChange={(event) => select({ periods: Number(event.target.value) })}
              className={SELECT_CLASS}
            >
              {PERIOD_OPTIONS.map((count) => (
                <option key={count} value={count}>
                  {t("lastPeriods", { count })}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {data.fields.length === 0 || periods.length === 0 ? (
        <EmptyState icon={LineChart} title={t("emptyTitle")} description={t("emptyDescription")} />
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {data.fields.slice(0, KPI_COUNT).map((f) => {
              const series = seriesByKey.get(f.fieldKey);
              const values = series?.values ?? [];
              const { latest, latestIndex, previous, previousIndex } = latestTwo(values);
              const difference = change(previous, latest, Number(f.type));
              const target = series?.target ?? null;
              const kind = changeKind(Number(f.type));
              return (
                <div key={f.fieldKey} className="rounded-lg border border-line bg-surface p-5">
                  <p className="text-xs font-bold uppercase tracking-wide text-ink-muted">
                    {latestIndex >= 0 ? t("kpiLabel", { field: f.label, period: periods[latestIndex].periodLabel }) : f.label}
                  </p>
                  <p className="mt-2 text-3xl font-bold tabular-nums text-ink">{format(f)(latest)}</p>
                  {target != null ? (
                    <p className={cn("mt-1 text-sm font-semibold", latest != null && latest >= target ? "text-green-700" : "text-status-late-fg")}>
                      {t("kpiTarget", { target: format(f)(target) })}
                    </p>
                  ) : difference != null && previousIndex >= 0 ? (
                    <p className={cn("mt-1 text-sm font-semibold", difference > 0 ? "text-green-700" : difference < 0 ? "text-danger" : "text-ink-muted")}>
                      {t(`kpiChange_${kind}`, { change: formatSigned(difference), period: periods[previousIndex].periodLabel })}
                    </p>
                  ) : (
                    <p className="mt-1 text-sm text-ink-muted">{t("kpiNoChange")}</p>
                  )}
                </div>
              );
            })}
          </div>

          {field && fieldSeries ? (
            <section className="rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="indicators-chart-title">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <h2 id="indicators-chart-title" className="text-lg font-bold text-ink">
                  {fieldSeries.target != null ? t("chartTitleTarget", { field: field.label }) : field.label}
                </h2>
                <div role="group" aria-label={t("chartModeLabel")} className="flex rounded-md bg-mint/60 p-1">
                  {(["line", "bars"] as const).map((option) => (
                    <button
                      key={option}
                      type="button"
                      aria-pressed={mode === option}
                      onClick={() => setMode(option)}
                      className={cn("h-10 rounded-sm px-4 text-sm font-semibold", mode === option ? "bg-surface text-ink shadow-sm" : "text-ink-muted hover:text-ink")}
                    >
                      {t(option === "line" ? "modeLine" : "modeBars")}
                    </button>
                  ))}
                </div>
              </div>
              <div className="mt-4">
                <IndicatorChart
                  mode={mode}
                  target={fieldSeries.target}
                  format={(value) => format(field)(value)}
                  labels={{
                    ariaLabel: t("chartAriaLabel", { field: field.label }),
                    target: fieldSeries.target != null ? t("targetLine", { target: format(field)(fieldSeries.target) }) : "",
                  }}
                  points={periods.map((period, index) => ({ periodLabel: period.periodLabel, value: fieldSeries.values[index] ?? null }))}
                />
              </div>
            </section>
          ) : null}

          {data.children.length > 0 ? (
            <MapCard
              reportTypeId={data.reportTypeId ?? undefined}
              fieldKey={data.fieldKey ?? undefined}
              unitId={node?.unitId}
              onOpenUnit={(unitId) => select({ unitId })}
            />
          ) : null}

          {field && data.children.length > 0 ? (
            <section aria-labelledby="indicators-children-title" className="flex flex-col gap-3">
              <h2 id="indicators-children-title" className="text-lg font-bold text-ink">
                {t("drillTitle", { level: data.childLevelPluralName ?? t("drillUnits"), unit: node?.name ?? "" })}
              </h2>
              <div className="overflow-x-auto rounded-lg border border-line bg-surface">
                <table className="w-full min-w-[640px] border-collapse text-sm">
                  <thead>
                    <tr className="border-b border-line text-xs font-bold uppercase tracking-wide text-ink-muted">
                      <th scope="col" className="px-4 py-3 text-left">
                        {t("columnUnit")}
                      </th>
                      <th scope="col" className="px-4 py-3 text-right">
                        {field.label}
                      </th>
                      <th scope="col" className="px-4 py-3 text-right">
                        {t("columnChange", { count: periods.length })}
                      </th>
                      <th scope="col" className="px-4 py-3 text-left">
                        {t("columnOfTarget")}
                      </th>
                      <th scope="col" className="px-4 py-3">
                        <span className="sr-only">{t("columnOpen")}</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.children.map((child) => {
                      const first = toNumber(child.first);
                      const latest = toNumber(child.latest);
                      const target = toNumber(child.target);
                      const difference = change(first, latest, Number(field.type));
                      const ofTarget = percentOfTarget(latest, target);
                      return (
                        <tr key={child.unitId} className="border-b border-line last:border-0">
                          <th scope="row" className="px-4 py-3.5 text-left">
                            <Link href={hrefFor({ unitId: child.unitId })} onClick={() => setSelection((s) => ({ ...s, unitId: child.unitId }))} className="font-semibold text-ink underline underline-offset-2">
                              {child.name}
                            </Link>
                          </th>
                          <td className="px-4 py-3.5 text-right tabular-nums text-ink">{format(field)(latest)}</td>
                          <td className={cn("px-4 py-3.5 text-right font-bold tabular-nums", difference == null || difference === 0 ? "text-ink-muted" : difference > 0 ? "text-green-700" : "text-danger")}>
                            {difference == null ? "—" : changeKind(Number(field.type)) === "percent" ? `${formatSigned(difference)}%` : formatSigned(difference)}
                          </td>
                          <td className="px-4 py-3.5">
                            {ofTarget == null ? (
                              <span className="text-ink-muted">—</span>
                            ) : (
                              <div className="flex items-center gap-3">
                                <div className="h-2 w-40 max-w-full rounded-full bg-line" aria-hidden="true">
                                  <div className={cn("h-2 rounded-full", ofTarget >= TARGET_ON_TRACK_PERCENT ? "bg-chart-on-time" : "bg-chart-late")} style={{ width: `${Math.min(ofTarget, 100)}%` }} />
                                </div>
                                <span className="font-bold tabular-nums text-ink">{ofTarget}%</span>
                              </div>
                            )}
                          </td>
                          <td className="px-4 py-3.5 text-right">
                            <ChevronRight className="inline size-4 text-ink-muted" aria-hidden="true" />
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
