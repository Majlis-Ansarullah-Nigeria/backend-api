"use client";

import { useQueries } from "@tanstack/react-query";
import { Calendar, ChevronLeft, ChevronRight, Plus, RefreshCw } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import type { ApiError } from "@/lib/api/client";
import type { ReportTypeSummary, SubmissionWindowSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetReportTypes } from "@/lib/api/generated/templates/templates";
import { getGetSubmissionWindowsQueryOptions } from "@/lib/api/generated/windows/windows";
import { cn } from "@/lib/utils";
import {
  buildCalendarEvents,
  buildMonthGrid,
  groupEventsByDate,
  toDateKey,
  type CalendarEvent,
  type CalendarEventType,
} from "@/features/windows/utils/calendar";
import { SchedulesPanel } from "./SchedulesPanel";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";

const EVENT_STYLES: Record<CalendarEventType, string> = {
  opens: "bg-status-acknowledged-bg text-status-acknowledged-fg",
  due: "bg-status-late-bg text-status-late-fg",
  closes: "bg-status-returned-bg text-status-returned-fg",
};

// A month label, not an instant — formatted in UTC so no zone can shift it into another month.
const MONTH_FORMATTER = new Intl.DateTimeFormat("en-GB", { month: "long", year: "numeric", timeZone: "UTC" });
const WEEKDAY_LABELS = ["MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"];

export function WindowsCalendarScreen() {
  const fmt = useOrgFormat();
  const t = useTranslations("Windows");
  const today = new Date();
  const [year, setYear] = useState(today.getFullYear());
  const [month, setMonth] = useState(today.getMonth());
  const [view, setView] = useState<"month" | "list">("month");
  const [reportTypeFilter, setReportTypeFilter] = useState("");
  const [showSchedules, setShowSchedules] = useState(false);

  const reportTypes = useGetReportTypes<ReportTypeSummary[], ApiError>({ includeArchived: false });

  const visibleReportTypes = (reportTypes.data ?? []).filter(
    (reportType) => !reportTypeFilter || reportType.id === reportTypeFilter,
  );

  const windowQueries = useQueries({
    queries: visibleReportTypes.map((reportType) => getGetSubmissionWindowsQueryOptions<SubmissionWindowSummary[], ApiError>(reportType.id)),
  });

  if (reportTypes.isPending) {
    return <p className="p-4 text-sm text-ink-muted sm:p-6">{t("loading")}</p>;
  }

  if (reportTypes.isError) {
    return (
      <div className="p-4 sm:p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => reportTypes.refetch()} />
      </div>
    );
  }

  const windowsLoading = windowQueries.some((query) => query.isPending);
  const windowsErrored = windowQueries.some((query) => query.isError);

  const events = windowsLoading
    ? []
    : buildCalendarEvents(
        visibleReportTypes.flatMap((reportType, index) =>
          (windowQueries[index]?.data ?? []).map((window) => ({ window, reportTypeId: reportType.id, reportTypeName: reportType.name })),
        ),
        (iso) => fmt.dayKey(iso),
      );
  const eventsByDate = groupEventsByDate(events);

  const goToMonth = (delta: number) => {
    const next = new Date(year, month + delta, 1);
    setYear(next.getFullYear());
    setMonth(next.getMonth());
  };

  const goToToday = () => {
    setYear(today.getFullYear());
    setMonth(today.getMonth());
  };

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-ink">{t("calendarTitle")}</h1>
          <p className="mt-1 text-sm text-ink-muted">{t("calendarSubtitle")}</p>
        </div>
        <div className="flex gap-2">
          <Button type="button" variant="outline" onClick={() => setShowSchedules((value) => !value)}>
            <RefreshCw className="size-4" aria-hidden="true" />
            {t("schedulesButton")}
          </Button>
          <Link href="/secretariat/windows/new">
            <Button type="button">
              <Plus className="size-4" aria-hidden="true" />
              {t("newWindow")}
            </Button>
          </Link>
        </div>
      </div>

      {showSchedules ? (
        <SchedulesPanel reportTypes={reportTypes.data} onClose={() => setShowSchedules(false)} />
      ) : null}

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <Button type="button" size="sm" variant="outline" aria-label={t("previousMonth")} onClick={() => goToMonth(-1)}>
            <ChevronLeft className="size-4" aria-hidden="true" />
          </Button>
          <h2 className="min-w-40 text-lg font-bold text-ink">{MONTH_FORMATTER.format(Date.UTC(year, month, 1))}</h2>
          <Button type="button" size="sm" variant="outline" aria-label={t("nextMonth")} onClick={() => goToMonth(1)}>
            <ChevronRight className="size-4" aria-hidden="true" />
          </Button>
          <Button type="button" size="sm" variant="outline" onClick={goToToday}>
            {t("today")}
          </Button>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <select
            value={reportTypeFilter}
            onChange={(event) => setReportTypeFilter(event.target.value)}
            aria-label={t("filterByReportType")}
            className="h-9 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("allReportTypes")}</option>
            {reportTypes.data.map((reportType) => (
              <option key={reportType.id} value={reportType.id}>
                {reportType.name}
              </option>
            ))}
          </select>

          <div className="flex gap-1 rounded-md bg-ground p-1">
            <ViewButton active={view === "month"} onClick={() => setView("month")} label={t("monthView")} />
            <ViewButton active={view === "list"} onClick={() => setView("list")} label={t("listView")} />
          </div>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-4 text-xs text-ink-muted">
        <LegendDot className="bg-status-acknowledged-fg" label={t("legendOpens")} />
        <LegendDot className="bg-status-late-fg" label={t("legendDue")} />
        <LegendDot className="bg-status-returned-fg" label={t("legendCloses")} />
      </div>

      {windowsErrored ? (
        <ErrorState title={t("loadErrorTitle")} onRetry={() => windowQueries.forEach((query) => query.refetch())} />
      ) : windowsLoading ? (
        <p className="text-sm text-ink-muted">{t("loading")}</p>
      ) : events.length === 0 ? (
        <EmptyState icon={Calendar} title={t("emptyTitle")} description={t("emptyDescription")} />
      ) : view === "month" ? (
        <MonthGrid year={year} month={month} eventsByDate={eventsByDate} />
      ) : (
        <ListView events={events} />
      )}
    </div>
  );
}

function MonthGrid({
  year,
  month,
  eventsByDate,
}: {
  year: number;
  month: number;
  eventsByDate: Map<string, CalendarEvent[]>;
}) {
  const fmt = useOrgFormat();
  const days = buildMonthGrid(year, month);

  return (
    <div className="overflow-hidden rounded-lg border border-line">
      <div className="grid grid-cols-7 border-b border-line bg-ground text-xs font-medium uppercase tracking-wide text-ink-muted">
        {WEEKDAY_LABELS.map((label) => (
          <div key={label} className="px-3 py-2">
            {label}
          </div>
        ))}
      </div>
      <div className="grid grid-cols-7">
        {days.map((day) => {
          const inMonth = day.getMonth() === month;
          const key = toDateKey(day.toISOString());
          const dayEvents = eventsByDate.get(key) ?? [];
          const isToday = key === fmt.dayKey(new Date());

          return (
            <div key={key} className={cn("min-h-28 border-b border-r border-line p-2", !inMonth && "bg-ground/50")}>
              <span
                className={cn(
                  "inline-flex size-6 items-center justify-center rounded-full text-sm",
                  isToday && "bg-green-600 font-bold text-white",
                  !inMonth && "text-ink-muted",
                )}
              >
                {day.getDate()}
              </span>
              <div className="mt-1 flex flex-col gap-1">
                {dayEvents.map((event, index) => (
                  <Link
                    key={`${event.windowId}-${event.type}-${index}`}
                    href={`/secretariat/windows/${event.windowId}?reportTypeId=${event.reportTypeId}`}
                    className={cn("truncate rounded-sm px-1.5 py-0.5 text-xs font-medium", EVENT_STYLES[event.type])}
                    title={event.label}
                  >
                    {event.label}
                  </Link>
                ))}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function ListView({ events }: { events: CalendarEvent[] }) {
  const fmt = useOrgFormat();
  const sorted = [...events].sort((a, b) => a.date.localeCompare(b.date));

  return (
    <div className="flex flex-col divide-y divide-line rounded-lg border border-line bg-surface">
      {sorted.map((event, index) => (
        <Link
          key={`${event.windowId}-${event.type}-${index}`}
          href={`/secretariat/windows/${event.windowId}?reportTypeId=${event.reportTypeId}`}
          className="flex items-center justify-between gap-3 px-4 py-3 hover:bg-ground"
        >
          <span className="text-sm text-ink">{event.label}</span>
          <span className={cn("rounded-full px-2 py-0.5 text-xs font-medium", EVENT_STYLES[event.type])}>
            {fmt.calendarDate(event.date)}
          </span>
        </Link>
      ))}
    </div>
  );
}

function ViewButton({ active, onClick, label }: { active: boolean; onClick: () => void; label: string }) {
  return (
    <Button type="button" variant={active ? "primary" : "ghost"} size="sm" aria-pressed={active} onClick={onClick}>
      {label}
    </Button>
  );
}

function LegendDot({ className, label }: { className: string; label: string }) {
  return (
    <span className="flex items-center gap-1.5">
      <span className={cn("size-2.5 rounded-full", className)} aria-hidden="true" />
      {label}
    </span>
  );
}
