"use client";

import { BarChart3, Download, FileSpreadsheet, FileText, History as HistoryIcon, Search } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { StatusChip } from "@/components/shared/StatusChip";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api/client";
import type { HistoryFilterOptions, HistoryPage, HistoryRow } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  createBulkExport,
  getHistoryReportExcel,
  getHistoryReportPdf,
  useGetHistory,
  useGetHistoryFilters,
} from "@/lib/api/generated/history/history";
import { downloadBlob } from "@/features/organization/utils/bulk-import";
import { HISTORY_STATUS_ORDER, historyStatusOptionLabel } from "@/features/history/utils/history-status";
import { BULK_EXPORT_FORMAT } from "@/features/exports/utils/export-status";
import { reportStatusChipVariant } from "@/features/submissions/utils/report-status";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";
import { FEATURE_MODULE, useFeatureFlags } from "@/features/admin/hooks/useFeatureFlags";


interface Filters {
  reportTypeId?: string;
  fromWindowId?: string;
  toWindowId?: string;
  status?: number;
}

export function HistoryScreen() {
  const t = useTranslations("History");
  const { isEnabled } = useFeatureFlags();
  const router = useRouter();

  const [filters, setFilters] = useState<Filters>({});
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [bulkExporting, setBulkExporting] = useState(false);
  const [bulkExportError, setBulkExportError] = useState<string | null>(null);

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

  const toggleSelected = (instanceId: string, checked: boolean) => {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(instanceId);
      } else {
        next.delete(instanceId);
      }
      return next;
    });
  };

  const runBulkExport = async (format: number) => {
    setBulkExporting(true);
    setBulkExportError(null);
    try {
      const summary = await createBulkExport({ instanceIds: [...selectedIds], format });
      router.push(`/exports/${summary.id}`);
    } catch (error) {
      setBulkExportError(error instanceof ApiError ? error.message : t("bulkExportFailed"));
    } finally {
      setBulkExporting(false);
    }
  };

  const totalCount = history.data ? Number(history.data.totalCount) : 0;
  const rows = history.data?.rows ?? [];
  const hasPrevious = page > 1;
  const hasNext = page * pageSize < totalCount;

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
          <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
        </div>
        {isEnabled(FEATURE_MODULE.ComparisonAndVersions) ? (
          <Link
            href="/history/compare"
            className="inline-flex h-11 items-center gap-2 rounded-sm border border-line-strong bg-surface px-4 text-sm font-semibold text-ink hover:bg-ground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-green-600"
          >
            <BarChart3 className="size-4" aria-hidden="true" />
            {t("comparePeriodsAction")}
          </Link>
        ) : null}
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

      {bulkExportError && <p className="text-sm text-danger">{bulkExportError}</p>}

      {selectedIds.size > 0 && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg bg-forest-900 px-4 py-3 text-white">
          <span className="text-sm font-semibold">{t("selectedCount", { count: selectedIds.size })}</span>
          <div className="flex flex-wrap items-center gap-4">
            <Button type="button" variant="accent" disabled={bulkExporting} onClick={() => runBulkExport(BULK_EXPORT_FORMAT.ZipOfPdfs)}>
              <Download className="size-4" aria-hidden="true" />
              {t("downloadZipAction")}
            </Button>
            <Button
              type="button"
              variant="outline"
              disabled={bulkExporting}
              onClick={() => runBulkExport(BULK_EXPORT_FORMAT.ExcelWorkbook)}
              className="border-white/30 bg-transparent text-white hover:bg-white/10"
            >
              <FileSpreadsheet className="size-4" aria-hidden="true" />
              {t("exportExcelAction")}
            </Button>
            <button
              type="button"
              onClick={() => setSelectedIds(new Set())}
              className="text-sm font-medium text-white underline-offset-2 hover:underline"
            >
              {t("clearSelection")}
            </button>
          </div>
        </div>
      )}

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
                  <th className="w-10 px-4 py-3">
                    <span className="sr-only">{t("columnSelect")}</span>
                  </th>
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
                  <HistoryTableRow
                    key={row.instanceId}
                    row={row}
                    selected={selectedIds.has(row.instanceId)}
                    onToggleSelected={(checked) => toggleSelected(row.instanceId, checked)}
                  />
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

function HistoryTableRow({
  row,
  selected,
  onToggleSelected,
}: {
  row: HistoryRow;
  selected: boolean;
  onToggleSelected: (checked: boolean) => void;
}) {
  const fmt = useOrgFormat();
  const t = useTranslations("History");
  const { isEnabled } = useFeatureFlags();
  const href = row.isObserved ? `/units/${row.unitId}/reports/${row.instanceId}` : `/reports/${row.instanceId}/edit?unit=${row.unitId}`;
  const [downloading, setDownloading] = useState<"pdf" | "excel" | null>(null);

  const download = async (format: "pdf" | "excel") => {
    setDownloading(format);
    try {
      const blob =
        format === "pdf"
          ? ((await getHistoryReportPdf(row.instanceId)) as unknown as Blob)
          : ((await getHistoryReportExcel(row.instanceId)) as unknown as Blob);
      downloadBlob(blob, format === "pdf" ? "report.pdf" : "report.xlsx");
    } finally {
      setDownloading(null);
    }
  };

  return (
    <tr className="border-b border-line last:border-0">
      <td className="px-4 py-3.5">
        <Checkbox
          checked={selected}
          onCheckedChange={(checked) => onToggleSelected(checked === true)}
          aria-label={t("selectRowLabel", { report: row.reportTypeName, period: row.periodLabel })}
        />
      </td>
      <td className="px-4 py-3.5">
        <p className="font-semibold text-ink">{row.reportTypeName}</p>
        <p className="text-xs text-ink-muted">{row.periodLabel}</p>
      </td>
      <td className="px-4 py-3.5 text-ink">{row.unitName}</td>
      <td className="px-4 py-3.5 text-ink">{row.submittedAt ? fmt.date(row.submittedAt) : "—"}</td>
      <td className="px-4 py-3.5">
        <StatusChip status={reportStatusChipVariant(row.status)} />
      </td>
      <td className="px-4 py-3.5 text-ink">{row.submittedByLabel}</td>
      <td className="px-4 py-3.5">
        <div className="flex items-center justify-end gap-3">
          <Link href={href} className="text-sm font-medium text-green-700 underline-offset-2 hover:underline">
            {t("viewAction")}
          </Link>
          {isEnabled(FEATURE_MODULE.ComparisonAndVersions) ? (
            <Link
              href={`/history/${row.instanceId}/versions`}
              aria-label={t("versionsActionLabel", { report: row.reportTypeName, period: row.periodLabel })}
              className="text-sm font-medium text-green-700 underline-offset-2 hover:underline"
            >
              {t("versionsAction")}
            </Link>
          ) : null}
          <button
            type="button"
            disabled={downloading !== null}
            onClick={() => download("pdf")}
            aria-label={t("downloadPdfAction")}
            title={t("downloadPdfAction")}
            className="text-ink-muted hover:text-ink disabled:opacity-50"
          >
            <FileText className="size-4" aria-hidden="true" />
          </button>
          <button
            type="button"
            disabled={downloading !== null}
            onClick={() => download("excel")}
            aria-label={t("downloadExcelAction")}
            title={t("downloadExcelAction")}
            className="text-ink-muted hover:text-ink disabled:opacity-50"
          >
            <FileSpreadsheet className="size-4" aria-hidden="true" />
          </button>
        </div>
      </td>
    </tr>
  );
}
