"use client";

import { History as HistoryIcon, Search } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useEffect, useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { StatusChip } from "@/components/shared/StatusChip";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import type { ApiError } from "@/lib/api/client";
import type { HistoryFilterOptions, HistoryPage, HistoryRow } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetHistory, useGetHistoryFilters } from "@/lib/api/generated/history/history";
import { HISTORY_STATUS_ORDER, historyStatusOptionLabel } from "@/features/history/utils/history-status";
import { reportStatusChipVariant } from "@/features/submissions/utils/report-status";

const DATE_FORMATTER = new Intl.DateTimeFormat("en", { day: "numeric", month: "short", year: "numeric" });

interface Filters {
  reportTypeId?: string;
  fromWindowId?: string;
  toWindowId?: string;
  status?: number;
}

export function HistoryScreen() {
  const t = useTranslations("History");

  const [filters, setFilters] = useState<Filters>({});
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // A short debounce so full-text search doesn't fire a request per keystroke.
  useEffect(() => {
    const timeout = setTimeout(() => {
      setSearch(searchDraft);
      setPage(1);
    }, 300);
    return () => clearTimeout(timeout);
  }, [searchDraft]);

  const filterOptions = useGetHistoryFilters<HistoryFilterOptions, ApiError>();
  const history = useGetHistory<HistoryPage, ApiError>({
    reportTypeId: filters.reportTypeId,
    fromWindowId: filters.fromWindowId,
    toWindowId: filters.toWindowId,
    status: filters.status,
    search: search || undefined,
    page,
    pageSize,
  });

  const updateFilter = (patch: Partial<Filters>) => {
    setFilters((current) => ({ ...current, ...patch }));
    setPage(1);
  };

  const totalCount = history.data ? Number(history.data.totalCount) : 0;
  const rows = history.data?.rows ?? [];
  const hasPrevious = page > 1;
  const hasNext = page * pageSize < totalCount;

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
      </div>

      <div className="flex flex-wrap items-end gap-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="history-type">{t("typeLabel")}</Label>
          <select
            id="history-type"
            value={filters.reportTypeId ?? ""}
            onChange={(event) => updateFilter({ reportTypeId: event.target.value || undefined })}
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("allReportTypesOption")}</option>
            {(filterOptions.data?.reportTypes ?? []).map((reportType) => (
              <option key={reportType.id} value={reportType.id}>
                {reportType.name}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="history-from">{t("fromLabel")}</Label>
          <select
            id="history-from"
            value={filters.fromWindowId ?? ""}
            onChange={(event) => updateFilter({ fromWindowId: event.target.value || undefined })}
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("anyOption")}</option>
            {(filterOptions.data?.periods ?? []).map((period) => (
              <option key={period.windowId} value={period.windowId}>
                {period.periodLabel}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="history-to">{t("toLabel")}</Label>
          <select
            id="history-to"
            value={filters.toWindowId ?? ""}
            onChange={(event) => updateFilter({ toWindowId: event.target.value || undefined })}
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("anyOption")}</option>
            {(filterOptions.data?.periods ?? []).map((period) => (
              <option key={period.windowId} value={period.windowId}>
                {period.periodLabel}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="history-status">{t("statusLabel")}</Label>
          <select
            id="history-status"
            value={filters.status ?? ""}
            onChange={(event) => updateFilter({ status: event.target.value === "" ? undefined : Number(event.target.value) })}
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("anyOption")}</option>
            {HISTORY_STATUS_ORDER.map((status) => (
              <option key={status} value={status}>
                {historyStatusOptionLabel(t, status)}
              </option>
            ))}
          </select>
        </div>

        <div className="flex min-w-60 flex-1 flex-col gap-1.5">
          <Label htmlFor="history-search">{t("searchLabel")}</Label>
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-muted" aria-hidden="true" />
            <input
              id="history-search"
              value={searchDraft}
              onChange={(event) => setSearchDraft(event.target.value)}
              placeholder={t("searchPlaceholder")}
              className="h-11 w-full rounded-sm border border-line-strong bg-surface pl-9 pr-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
            />
          </div>
        </div>
      </div>

      {history.isPending ? (
        <p className="text-sm text-ink-muted">{t("loading")}</p>
      ) : history.isError ? (
        <ErrorState title={t("loadErrorTitle")} onRetry={() => history.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState icon={HistoryIcon} title={t("emptyTitle")} description={t("emptyDescription")} />
      ) : (
        <>
          <div className="overflow-x-auto rounded-lg border border-line bg-surface">
            <table className="w-full min-w-[720px] border-collapse text-sm">
              <thead>
                <tr className="border-b border-line text-left text-xs font-bold uppercase tracking-wide text-ink-muted">
                  <th className="px-4 py-3">{t("columnReport")}</th>
                  <th className="px-4 py-3">{t("columnUnit")}</th>
                  <th className="px-4 py-3">{t("columnSubmitted")}</th>
                  <th className="px-4 py-3">{t("columnStatus")}</th>
                  <th className="px-4 py-3">{t("columnBy")}</th>
                  <th className="px-4 py-3 text-right">{t("columnActions")}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <HistoryTableRow key={row.instanceId} row={row} />
                ))}
              </tbody>
            </table>
          </div>

          <div className="flex items-center justify-between">
            <span className="text-sm text-ink-muted">
              {t("showingCount", { shown: rows.length, total: totalCount })}
            </span>
            <div className="flex gap-2">
              <Button type="button" variant="outline" disabled={!hasPrevious} onClick={() => setPage((p) => p - 1)}>
                {t("previousPage")}
              </Button>
              <Button type="button" variant="outline" disabled={!hasNext} onClick={() => setPage((p) => p + 1)}>
                {t("nextPage")}
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function HistoryTableRow({ row }: { row: HistoryRow }) {
  const t = useTranslations("History");
  const href = row.isObserved ? `/units/${row.unitId}/reports/${row.instanceId}` : `/reports/${row.instanceId}/edit?unit=${row.unitId}`;

  return (
    <tr className="border-b border-line last:border-0">
      <td className="px-4 py-3.5">
        <p className="font-semibold text-ink">{row.reportTypeName}</p>
        <p className="text-xs text-ink-muted">{row.periodLabel}</p>
      </td>
      <td className="px-4 py-3.5 text-ink">{row.unitName}</td>
      <td className="px-4 py-3.5 text-ink">{row.submittedAt ? DATE_FORMATTER.format(new Date(row.submittedAt)) : "—"}</td>
      <td className="px-4 py-3.5">
        <StatusChip status={reportStatusChipVariant(row.status)} />
      </td>
      <td className="px-4 py-3.5 text-ink">{row.submittedByLabel}</td>
      <td className="px-4 py-3.5 text-right">
        <Link href={href} className="text-sm font-medium text-green-700 underline-offset-2 hover:underline">
          {t("viewAction")}
        </Link>
      </td>
    </tr>
  );
}
