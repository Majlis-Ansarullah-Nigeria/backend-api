"use client";

import { Download as DownloadIcon, FileText, Table2 } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { StatusChip } from "@/components/shared/StatusChip";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api/client";
import type { ExportSummary, ReportTypeSummary, SubmissionWindowSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { createOrganizationExport, useGetMyExports } from "@/lib/api/generated/exports/exports";
import { useGetReportTypes } from "@/lib/api/generated/templates/templates";
import { useGetSubmissionWindows } from "@/lib/api/generated/windows/windows";
import { HISTORY_STATUS_ORDER, historyStatusOptionLabel } from "@/features/history/utils/history-status";
import { BULK_EXPORT_FORMAT, BULK_EXPORT_STATUS, exportStatusChipVariant } from "@/features/exports/utils/export-status";

const DATE_TIME_FORMATTER = new Intl.DateTimeFormat("en", { day: "numeric", month: "short", hour: "numeric", minute: "2-digit" });

export function SecretariatExportsScreen() {
  const t = useTranslations("Exports");
  const tHistory = useTranslations("History");

  const [reportTypeId, setReportTypeId] = useState("");
  const [windowId, setWindowId] = useState("");
  const [status, setStatus] = useState<number | undefined>(undefined);
  const [format, setFormat] = useState<number>(BULK_EXPORT_FORMAT.ExcelWorkbook);
  const [starting, setStarting] = useState(false);
  const [startError, setStartError] = useState<string | null>(null);
  const [justStartedId, setJustStartedId] = useState<string | null>(null);

  const reportTypes = useGetReportTypes<ReportTypeSummary[], ApiError>();
  const windows = useGetSubmissionWindows<SubmissionWindowSummary[], ApiError>(reportTypeId, {
    query: { enabled: reportTypeId !== "" },
  });
  const recentExports = useGetMyExports<ExportSummary[], ApiError>();

  const startExport = async () => {
    setStarting(true);
    setStartError(null);
    try {
      const summary = await createOrganizationExport({
        reportTypeId: reportTypeId || null,
        fromWindowId: windowId || null,
        toWindowId: windowId || null,
        status: status ?? null,
        format,
      });
      setJustStartedId(summary.id);
      await recentExports.refetch();
    } catch (error) {
      setStartError(error instanceof ApiError ? error.message : t("startExportFailed"));
    } finally {
      setStarting(false);
    }
  };

  const exports = recentExports.data ?? [];

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
      </div>

      <div className="flex flex-col gap-5 rounded-lg border border-line bg-surface p-5">
        <h2 className="text-lg font-bold text-ink">{t("newExportTitle")}</h2>

        <div className="flex flex-wrap items-end gap-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="export-type">{t("reportTypeLabel")}</Label>
            <select
              id="export-type"
              value={reportTypeId}
              onChange={(event) => {
                setReportTypeId(event.target.value);
                setWindowId("");
              }}
              className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
            >
              <option value="">{tHistory("allReportTypesOption")}</option>
              {(reportTypes.data ?? []).map((reportType) => (
                <option key={reportType.id} value={reportType.id}>
                  {reportType.name}
                </option>
              ))}
            </select>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="export-period">{t("periodLabel")}</Label>
            <select
              id="export-period"
              value={windowId}
              disabled={reportTypeId === ""}
              onChange={(event) => setWindowId(event.target.value)}
              className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40 disabled:opacity-50"
            >
              <option value="">{tHistory("anyOption")}</option>
              {(windows.data ?? []).map((window) => (
                <option key={window.id} value={window.id}>
                  {window.periodLabel}
                </option>
              ))}
            </select>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="export-status">{t("statusLabel")}</Label>
            <select
              id="export-status"
              value={status ?? ""}
              onChange={(event) => setStatus(event.target.value === "" ? undefined : Number(event.target.value))}
              className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
            >
              <option value="">{tHistory("anyOption")}</option>
              {HISTORY_STATUS_ORDER.map((value) => (
                <option key={value} value={value}>
                  {historyStatusOptionLabel(tHistory, value)}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <FormatOption
            icon={Table2}
            title={t("formatExcelTitle")}
            description={t("formatExcelDescription")}
            selected={format === BULK_EXPORT_FORMAT.ExcelWorkbook}
            onSelect={() => setFormat(BULK_EXPORT_FORMAT.ExcelWorkbook)}
          />
          <FormatOption
            icon={FileText}
            title={t("formatZipTitle")}
            description={t("formatZipDescription")}
            selected={format === BULK_EXPORT_FORMAT.ZipOfPdfs}
            onSelect={() => setFormat(BULK_EXPORT_FORMAT.ZipOfPdfs)}
          />
        </div>

        {startError && <p className="text-sm text-danger">{startError}</p>}
        {justStartedId && (
          <p className="text-sm text-green-700">
            {t("startedMessage")}{" "}
            <Link href={`/exports/${justStartedId}`} className="font-medium underline-offset-2 hover:underline">
              {t("viewExportLink")}
            </Link>
          </p>
        )}

        <div className="flex justify-end">
          <Button type="button" onClick={startExport} disabled={starting}>
            <DownloadIcon className="size-4" aria-hidden="true" />
            {t("startExportAction")}
          </Button>
        </div>
      </div>

      <div>
        <h2 className="mb-3 text-lg font-bold text-ink">{t("recentExportsTitle")}</h2>
        {recentExports.isPending ? (
          <p className="text-sm text-ink-muted">{t("loading")}</p>
        ) : recentExports.isError ? (
          <ErrorState title={t("loadErrorTitle")} onRetry={() => recentExports.refetch()} />
        ) : exports.length === 0 ? (
          <EmptyState icon={DownloadIcon} title={t("emptyTitle")} description={t("emptyDescription")} />
        ) : (
          <div className="overflow-x-auto rounded-lg border border-line bg-surface">
            <table className="w-full min-w-[640px] border-collapse text-sm">
              <thead>
                <tr className="border-b border-line text-left text-xs font-bold uppercase tracking-wide text-ink-muted">
                  <th className="px-4 py-3">{t("columnFormat")}</th>
                  <th className="px-4 py-3">{t("columnStatus")}</th>
                  <th className="px-4 py-3">{t("columnStarted")}</th>
                  <th className="px-4 py-3">{t("columnRequestedBy")}</th>
                  <th className="px-4 py-3 text-right">{t("columnActions")}</th>
                </tr>
              </thead>
              <tbody>
                {exports.map((exportSummary) => (
                  <tr key={exportSummary.id} className="border-b border-line last:border-0">
                    <td className="px-4 py-3.5 text-ink">
                      {exportSummary.format === BULK_EXPORT_FORMAT.ZipOfPdfs ? t("formatZipTitle") : t("formatExcelTitle")}
                    </td>
                    <td className="px-4 py-3.5">
                      <StatusChip status={exportStatusChipVariant(exportSummary.status)} label={exportStatusLabel(t, exportSummary.status)} />
                    </td>
                    <td className="px-4 py-3.5 text-ink">{DATE_TIME_FORMATTER.format(new Date(exportSummary.createdAt))}</td>
                    <td className="px-4 py-3.5 text-ink">{exportSummary.requestedByName || "—"}</td>
                    <td className="px-4 py-3.5 text-right">
                      <Link href={`/exports/${exportSummary.id}`} className="text-sm font-medium text-green-700 underline-offset-2 hover:underline">
                        {t("viewAction")}
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

function FormatOption({
  icon: Icon,
  title,
  description,
  selected,
  onSelect,
}: {
  icon: typeof FileText;
  title: string;
  description: string;
  selected: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      aria-pressed={selected}
      className={`flex flex-col items-start gap-2 rounded-lg border p-4 text-left transition-colors ${
        selected ? "border-green-600 bg-mint" : "border-line-strong bg-surface hover:border-line-strong/80"
      }`}
    >
      <Icon className="size-5 text-green-700" aria-hidden="true" />
      <span className="font-semibold text-ink">{title}</span>
      <span className="text-xs text-ink-muted">{description}</span>
    </button>
  );
}

function exportStatusLabel(t: ReturnType<typeof useTranslations>, status: number): string {
  switch (status) {
    case BULK_EXPORT_STATUS.Processing:
      return t("statusProcessing");
    case BULK_EXPORT_STATUS.Ready:
      return t("statusReady");
    case BULK_EXPORT_STATUS.Failed:
      return t("statusFailed");
    case BULK_EXPORT_STATUS.Pending:
    default:
      return t("statusPending");
  }
}
