"use client";

import { AlertTriangle, ArrowRight, BarChart3, ChevronRight, Info, Pencil } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { FeatureAwareErrorState } from "@/components/shared/FeatureAwareErrorState";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api/client";
import type { ComparisonUnitOption, PeriodComparison } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetComparisonOptions, useGetPeriodComparison } from "@/lib/api/generated/history/history";
import { TrendChart } from "@/features/history/components/TrendChart";
import {
  buildInsights,
  type ComparableFigure,
  figureChange,
  formatDelta,
  formatFigure,
  formatPercent,
  type Insight,
  type SeriesPoint,
} from "@/features/history/utils/comparison";
import { cn } from "@/lib/utils";

const SELECT_CLASS =
  "h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40";

export interface CompareSelection {
  unitId?: string;
  reportTypeId?: string;
  periodA?: string;
  periodB?: string;
}

const toNumber = (value: number | string | null | undefined): number | null => (value == null ? null : Number(value));

/** FR-HIS-005: the "Compare periods" board — two periods of one unit's report side by side. */
export function CompareScreen({ initial }: { initial: CompareSelection }) {
  const t = useTranslations("Compare");
  const router = useRouter();
  const options = useGetComparisonOptions<ComparisonUnitOption[], ApiError>();

  const [selection, setSelection] = useState<CompareSelection>(initial);
  const [chartFieldKey, setChartFieldKey] = useState<string | undefined>();

  // Default to the first unit/report type the caller can compare, unless the URL already chose.
  const units = options.data ?? [];
  const unitId = selection.unitId ?? units[0]?.unitId;
  const unitOption = units.find((unit) => unit.unitId === unitId);
  const reportTypeId = selection.reportTypeId ?? unitOption?.reportTypes[0]?.id;

  const comparison = useGetPeriodComparison<PeriodComparison, ApiError>(
    { unitId: unitId ?? "", reportTypeId: reportTypeId ?? "", periodA: selection.periodA, periodB: selection.periodB },
    { query: { enabled: Boolean(unitId && reportTypeId) } },
  );

  const select = (patch: CompareSelection) => {
    const next = { ...selection, unitId, reportTypeId, ...patch };
    setSelection(next);
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(next)) {
      if (value) {
        params.set(key, value);
      }
    }
    router.replace(`/history/compare?${params.toString()}`);
  };

  const data = comparison.data;
  const figures: ComparableFigure[] = useMemo(
    () =>
      (data?.figures ?? []).map((figure) => ({
        fieldKey: figure.fieldKey,
        label: figure.label,
        type: Number(figure.type),
        periodA: toNumber(figure.periodA),
        periodB: toNumber(figure.periodB),
      })),
    [data],
  );
  const series: SeriesPoint[] = useMemo(
    () =>
      (data?.series ?? []).map((point) => ({
        periodLabel: point.periodLabel,
        values: Object.fromEntries(Object.entries(point.values).map(([key, value]) => [key, toNumber(value)])),
      })),
    [data],
  );

  const chartField = figures.find((figure) => figure.fieldKey === chartFieldKey) ?? figures[0];
  const periodLabel = (windowId: string | null | undefined) => {
    const period = data?.periods.find((p) => p.windowId === windowId);
    if (!period) {
      return "";
    }
    return period.isDraft ? t("draftPeriod", { period: period.periodLabel }) : period.periodLabel;
  };

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <nav aria-label={t("breadcrumbLabel")} className="flex items-center gap-2 text-sm">
        <Link href="/history" className="text-ink-muted hover:text-ink">
          {t("breadcrumbHistory")}
        </Link>
        <ChevronRight className="size-4 text-ink-muted" aria-hidden="true" />
        <span className="font-semibold text-ink" aria-current="page">
          {t("breadcrumbCompare")}
        </span>
      </nav>

      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-ink sm:text-3xl">{t("title")}</h1>
          {data ? (
            <p className="mt-1 text-sm text-ink-muted">
              {data.unitName} · {data.reportTypeName}
            </p>
          ) : null}
        </div>

        <div className="flex flex-wrap items-end gap-3">
          {units.length > 1 ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="compare-unit">{t("unitLabel")}</Label>
              <select
                id="compare-unit"
                value={unitId ?? ""}
                onChange={(event) => select({ unitId: event.target.value, reportTypeId: undefined, periodA: undefined, periodB: undefined })}
                className={SELECT_CLASS}
              >
                {units.map((unit) => (
                  <option key={unit.unitId} value={unit.unitId}>
                    {unit.unitName}
                  </option>
                ))}
              </select>
            </div>
          ) : null}

          {unitOption && unitOption.reportTypes.length > 1 ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="compare-type">{t("reportTypeLabel")}</Label>
              <select
                id="compare-type"
                value={reportTypeId ?? ""}
                onChange={(event) => select({ reportTypeId: event.target.value, periodA: undefined, periodB: undefined })}
                className={SELECT_CLASS}
              >
                {unitOption.reportTypes.map((reportType) => (
                  <option key={reportType.id} value={reportType.id}>
                    {reportType.name}
                  </option>
                ))}
              </select>
            </div>
          ) : null}

          {data && data.periods.length > 0 ? (
            <>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="compare-period-a">{t("periodALabel")}</Label>
                <select
                  id="compare-period-a"
                  value={data.periodAWindowId ?? ""}
                  onChange={(event) => select({ periodA: event.target.value, periodB: data.periodBWindowId ?? undefined })}
                  className={SELECT_CLASS}
                >
                  {data.periods.map((period) => (
                    <option key={period.windowId} value={period.windowId}>
                      {periodLabel(period.windowId)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="compare-period-b">{t("periodBLabel")}</Label>
                <select
                  id="compare-period-b"
                  value={data.periodBWindowId ?? ""}
                  onChange={(event) => select({ periodA: data.periodAWindowId ?? undefined, periodB: event.target.value })}
                  className={SELECT_CLASS}
                >
                  {data.periods.map((period) => (
                    <option key={period.windowId} value={period.windowId}>
                      {periodLabel(period.windowId)}
                    </option>
                  ))}
                </select>
              </div>
            </>
          ) : null}
        </div>
      </div>

      {options.isError || comparison.isError ? (
        <FeatureAwareErrorState
          error={options.error ?? comparison.error}
          title={t("loadErrorTitle")}
          onRetry={() => (options.isError ? options.refetch() : comparison.refetch())}
        />
      ) : options.isPending || (comparison.isPending && Boolean(unitId && reportTypeId)) ? (
        <p className="text-sm text-ink-muted">{t("loading")}</p>
      ) : !data || data.periods.length < 2 ? (
        <EmptyState icon={BarChart3} title={t("emptyTitle")} description={t("emptyDescription")} />
      ) : (
        <>
          <div className="grid gap-6 lg:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
            <section className="rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="compare-trend-title">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <h2 id="compare-trend-title" className="text-lg font-bold text-ink">
                  {chartField ? t("trendTitle", { field: chartField.label, count: series.length }) : t("trendTitleEmpty")}
                </h2>
                {figures.length > 1 ? (
                  <select
                    aria-label={t("trendFieldLabel")}
                    value={chartField?.fieldKey ?? ""}
                    onChange={(event) => setChartFieldKey(event.target.value)}
                    className={SELECT_CLASS}
                  >
                    {figures.map((figure) => (
                      <option key={figure.fieldKey} value={figure.fieldKey}>
                        {figure.label}
                      </option>
                    ))}
                  </select>
                ) : null}
              </div>
              {chartField ? (
                <div className="mt-4">
                  <TrendChart
                    fieldType={chartField.type}
                    ariaLabel={t("trendAriaLabel", { field: chartField.label })}
                    points={series.map((point) => ({ periodLabel: point.periodLabel, value: point.values[chartField.fieldKey] ?? null }))}
                  />
                </div>
              ) : (
                <p className="mt-4 text-sm text-ink-muted">{t("noFigures")}</p>
              )}
            </section>

            <section className="rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="compare-changes-title">
              <h2 id="compare-changes-title" className="text-lg font-bold text-ink">
                {t("whatChangedTitle")}
              </h2>
              <ul className="mt-4 flex flex-col gap-3">
                {buildInsights(figures, series, data.changedTextSections, chartField?.fieldKey).map((insight, index) => (
                  <InsightItem key={index} insight={insight} fieldType={chartField?.type ?? 0} />
                ))}
              </ul>
            </section>
          </div>

          {figures.length > 0 ? (
            <div className="overflow-x-auto rounded-lg border border-line bg-surface">
              <table className="w-full min-w-[560px] border-collapse text-sm">
                <thead>
                  <tr className="border-b border-line text-xs font-bold uppercase tracking-wide text-ink-muted">
                    <th scope="col" className="px-4 py-3 text-left">
                      {t("columnField")}
                    </th>
                    <th scope="col" className="px-4 py-3 text-right">
                      {periodLabel(data.periodAWindowId)}
                    </th>
                    <th scope="col" className="px-4 py-3 text-right">
                      {periodLabel(data.periodBWindowId)}
                    </th>
                    <th scope="col" className="px-4 py-3 text-right">
                      {t("columnChange")}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {figures.map((figure) => (
                    <FigureRow key={figure.fieldKey} figure={figure} />
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </>
      )}
    </div>
  );
}

function FigureRow({ figure }: { figure: ComparableFigure }) {
  const change = figureChange(figure);
  const tone = change == null || change.delta === 0 ? "text-ink" : change.delta > 0 ? "text-green-700" : "text-danger";

  return (
    <tr className="border-b border-line last:border-0">
      <th scope="row" className="px-4 py-3.5 text-left font-normal text-ink">
        {figure.label}
      </th>
      <td className="px-4 py-3.5 text-right tabular-nums text-ink">{formatFigure(figure.periodA, figure.type)}</td>
      <td className="px-4 py-3.5 text-right tabular-nums text-ink">{formatFigure(figure.periodB, figure.type)}</td>
      <td className={cn("px-4 py-3.5 text-right font-bold tabular-nums", tone)}>
        {change == null ? (
          "—"
        ) : (
          <>
            {formatDelta(change.delta, figure.type)}
            {change.percent != null && change.delta !== 0 ? (
              <span className="ml-1.5 text-xs font-medium">({formatPercent(change.percent)})</span>
            ) : null}
          </>
        )}
      </td>
    </tr>
  );
}

function InsightItem({ insight, fieldType }: { insight: Insight; fieldType: number }) {
  const t = useTranslations("Compare");

  switch (insight.kind) {
    case "rise":
      return (
        <li className="flex gap-3 rounded-md bg-mint p-3 text-sm font-medium text-green-700">
          <ArrowRight className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          {insight.isLargestRise
            ? t("insightLargestRise", { field: insight.label, delta: formatFigure(insight.delta, fieldType), count: insight.periods })
            : t("insightRise", { field: insight.label, delta: formatFigure(insight.delta, fieldType) })}
        </li>
      );
    case "fall":
      return (
        <li className="flex gap-3 rounded-md bg-status-returned-bg p-3 text-sm font-medium text-status-returned-fg">
          <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          {t("insightFall", { field: insight.label, from: formatFigure(insight.from, 0), to: formatFigure(insight.to, 0) })}
        </li>
      );
    case "text":
      return (
        <li className="flex gap-3 rounded-md bg-ground p-3 text-sm text-ink">
          <Pencil className="mt-0.5 size-4 shrink-0 text-ink-muted" aria-hidden="true" />
          {t("insightText", { count: insight.sections })}
        </li>
      );
    default:
      return (
        <li className="flex gap-3 rounded-md bg-ground p-3 text-sm text-ink">
          <Info className="mt-0.5 size-4 shrink-0 text-ink-muted" aria-hidden="true" />
          {t("insightNone")}
        </li>
      );
  }
}
