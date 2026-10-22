"use client";

import { FileText } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { ErrorState } from "@/components/shared/ErrorState";
import { ApiError } from "@/lib/api/client";
import type { LeadershipOverview } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetLeadershipOverview } from "@/lib/api/generated/dashboards/dashboards";
import { isOnTrack, percentOf } from "@/features/dashboards/utils/compliance";
import { change, changeKind, formatIndicator, formatSigned } from "@/features/dashboards/utils/indicators";
import { cn } from "@/lib/utils";

// Matches backend/src/HRS.Application/Features/Analytics/GetLeadershipOverviewQuery.cs's enum.
export const LEADERSHIP_PERIOD = {
  Current: 0,
  Previous: 1,
  Year: 2,
} as const;

const DAY_MONTH = new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", timeZone: "Africa/Lagos" });

const toNumber = (value: number | string | null | undefined): number | null => (value == null ? null : Number(value));

/** The "Leadership overview (mobile)" board — Leadership's home (FR-DSH-002). */
export function LeadershipOverviewScreen() {
  const t = useTranslations("Leadership");
  const [period, setPeriod] = useState<number>(LEADERSHIP_PERIOD.Current);
  const query = useGetLeadershipOverview<LeadershipOverview, ApiError>({ period });

  if (query.isPending) {
    return <div className="p-4 text-sm text-ink-muted">{t("loading")}</div>;
  }

  if (query.isError) {
    return (
      <div className="p-4">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => query.refetch()} />
      </div>
    );
  }

  const data = query.data;
  const received = Number(data.received);
  const expected = Number(data.expected);
  const percent = percentOf(received, expected);
  const isYear = Number(data.period) === LEADERSHIP_PERIOD.Year;
  const tabs: { value: number; label: string }[] = [];
  if (data.currentPeriodLabel) {
    tabs.push({ value: LEADERSHIP_PERIOD.Current, label: data.currentPeriodLabel });
  }
  if (data.previousPeriodLabel) {
    tabs.push({ value: LEADERSHIP_PERIOD.Previous, label: data.previousPeriodLabel });
  }
  if (data.currentPeriodLabel) {
    tabs.push({ value: LEADERSHIP_PERIOD.Year, label: t("yearTab") });
  }

  return (
    <div className="mx-auto flex max-w-xl flex-col gap-4 p-4">
      <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>

      {tabs.length > 0 ? (
        <div role="group" aria-label={t("periodLabel")} className="flex rounded-md bg-mint/60 p-1">
          {tabs.map((tab) => (
            <button
              key={tab.value}
              type="button"
              aria-pressed={period === tab.value}
              onClick={() => setPeriod(tab.value)}
              className={cn(
                "h-11 flex-1 rounded-sm px-3 text-sm font-semibold",
                period === tab.value ? "bg-surface text-ink shadow-sm" : "text-ink-muted hover:text-ink",
              )}
            >
              {tab.label}
            </button>
          ))}
        </div>
      ) : null}

      <section className="flex items-center gap-5 rounded-lg bg-forest-900 p-5 text-white" aria-labelledby="leadership-received">
        <Ring percent={percent} />
        <div>
          <p id="leadership-received" className="text-xs font-bold uppercase tracking-wide text-white/70">
            {t("receivedLabel")}
          </p>
          <p className="text-3xl font-bold tabular-nums">{t("receivedValue", { received, expected })}</p>
          <p className="text-sm text-white/80">
            {isYear
              ? t("receivedYear")
              : data.closesAt
                ? t(new Date(data.closesAt) < new Date() ? "windowClosed" : "windowCloses", { date: DAY_MONTH.format(new Date(data.closesAt)) })
                : null}
          </p>
        </div>
      </section>

      <div className="grid grid-cols-2 gap-3">
        {data.figures.map((figure) => {
          const value = toNumber(figure.value);
          const compare = toNumber(figure.compare);
          const type = Number(figure.type);
          const difference = change(compare, value, type);
          const suffix = changeKind(type) === "percent" ? "%" : changeKind(type) === "points" ? ` ${t("points")}` : "";
          return (
            <div key={figure.fieldKey} className="rounded-lg border border-line bg-surface p-4">
              <p className="text-xs font-bold uppercase tracking-wide text-ink-muted">{figure.label}</p>
              <p className="mt-1 text-2xl font-bold tabular-nums text-ink">{formatIndicator(value, type, figure.unitLabel)}</p>
              <p className={cn("text-sm", difference == null || difference === 0 ? "text-ink-muted" : difference > 0 ? "text-green-700" : "text-danger")}>
                {difference == null
                  ? value == null
                    ? t("noFigure")
                    : (data.periodLabel ?? t("yearTab"))
                  : t(isYear ? "changeOverYear" : "changeVsPrevious", { change: `${formatSigned(difference)}${suffix}` })}
              </p>
            </div>
          );
        })}
        <div className="rounded-lg border border-line bg-surface p-4">
          <p className="text-xs font-bold uppercase tracking-wide text-ink-muted">{t("onTimeLabel")}</p>
          <p className="mt-1 text-2xl font-bold tabular-nums text-ink">{data.onTimePercent == null ? "—" : `${toNumber(data.onTimePercent)}%`}</p>
          <p className="text-sm text-green-700">{data.onTimePeriodLabel ?? t("noClosedPeriod")}</p>
        </div>
      </div>

      {data.zones.length > 0 ? (
        <section className="rounded-lg border border-line bg-surface p-4" aria-labelledby="leadership-zones">
          <h2 id="leadership-zones" className="text-lg font-bold text-ink">
            {isYear ? t("zonesTitleCurrent", { period: data.currentPeriodLabel ?? "" }) : t("zonesTitle", { period: data.periodLabel ?? "" })}
          </h2>
          <ul className="mt-3 flex flex-col gap-3">
            {data.zones.map((zone) => {
              const zonePercent = percentOf(Number(zone.submitted), Number(zone.expected));
              return (
                <li key={zone.groupUnitId ?? zone.name} className="grid grid-cols-[minmax(0,7rem)_1fr_3rem] items-center gap-3 text-sm">
                  <span className="truncate text-ink">{zone.name}</span>
                  <div className="h-2 rounded-full bg-line" aria-hidden="true">
                    <div
                      className={cn("h-2 rounded-full", isOnTrack(zonePercent) ? "bg-chart-on-time" : "bg-chart-late")}
                      style={{ width: `${zonePercent}%` }}
                    />
                  </div>
                  <span className="text-right font-bold tabular-nums text-ink">{zonePercent}%</span>
                </li>
              );
            })}
          </ul>
        </section>
      ) : null}
      {data.currentWindowId ? (
        <Link
          href={`/compiled/${data.currentWindowId}`}
          className="inline-flex h-14 items-center justify-center gap-2 rounded-lg bg-green-600 px-4 text-base font-semibold text-white hover:bg-green-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-green-600 focus-visible:ring-offset-2"
        >
          <FileText className="size-5" aria-hidden="true" />
          {t("openCompiledAction")}
        </Link>
      ) : null}
    </div>
  );
}

function Ring({ percent }: { percent: number }) {
  const radius = 34;
  const circumference = 2 * Math.PI * radius;
  return (
    <div className="relative size-24 shrink-0" aria-hidden="true">
      <svg viewBox="0 0 80 80" className="size-24 -rotate-90">
        <circle cx="40" cy="40" r={radius} fill="none" stroke="currentColor" strokeOpacity={0.2} strokeWidth={6} />
        <circle
          cx="40"
          cy="40"
          r={radius}
          fill="none"
          stroke="var(--color-lime)"
          strokeWidth={6}
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={circumference * (1 - Math.min(percent, 100) / 100)}
        />
      </svg>
      <span className="absolute inset-0 flex items-center justify-center text-xl font-bold">{percent}%</span>
    </div>
  );
}
