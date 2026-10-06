"use client";

import { Download } from "lucide-react";
import { useTranslations } from "next-intl";
import { useMemo, useState } from "react";
import type { BulkImportRow, BulkImportSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { cn } from "@/lib/utils";
import {
  BULK_IMPORT_OUTCOME,
  downloadBlob,
  errorReportCsv,
  filterRowsByTab,
  outcomeLabel,
  type BulkImportTab,
} from "@/features/organization/utils/bulk-import";

const OUTCOME_STYLES: Record<number, string> = {
  [BULK_IMPORT_OUTCOME.New]: "bg-status-acknowledged-bg text-status-acknowledged-fg",
  [BULK_IMPORT_OUTCOME.Update]: "bg-status-submitted-bg text-status-submitted-fg",
  [BULK_IMPORT_OUTCOME.Warning]: "bg-status-late-bg text-status-late-fg",
  [BULK_IMPORT_OUTCOME.Error]: "bg-status-returned-bg text-status-returned-fg",
};

export function BulkImportRowsTable({ summary }: { summary: BulkImportSummary }) {
  const t = useTranslations("Organization.import");
  const [tab, setTab] = useState<BulkImportTab>("all");

  const visibleRows = useMemo(() => filterRowsByTab(summary.rows, tab), [summary.rows, tab]);

  const handleDownloadErrors = () => {
    const csv = errorReportCsv(summary.rows);
    downloadBlob(new Blob([csv], { type: "text/csv" }), "import-errors.csv");
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <SummaryCard label={t("newUnits")} count={summary.newCount} description={t("newUnitsDescription")} />
        <SummaryCard label={t("updates")} count={summary.updateCount} description={t("updatesDescription")} />
        <SummaryCard label={t("warnings")} count={summary.warningCount} description={t("warningsDescription")} tone="warning" />
        <SummaryCard label={t("errors")} count={summary.errorCount} description={t("errorsDescription")} tone="danger" />
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex gap-1" role="tablist" aria-label={t("filterLabel")}>
          <FilterTab active={tab === "all"} onClick={() => setTab("all")} label={t("tabAll", { count: summary.totalRows })} />
          <FilterTab active={tab === "errors"} onClick={() => setTab("errors")} label={t("tabErrors", { count: summary.errorCount })} />
          <FilterTab active={tab === "warnings"} onClick={() => setTab("warnings")} label={t("tabWarnings", { count: summary.warningCount })} />
          <FilterTab active={tab === "updates"} onClick={() => setTab("updates")} label={t("tabUpdates", { count: summary.updateCount })} />
        </div>

        {Number(summary.errorCount) > 0 ? (
          <button
            type="button"
            onClick={handleDownloadErrors}
            className="flex items-center gap-1.5 text-sm font-medium text-green-700 hover:underline"
          >
            <Download className="size-4" aria-hidden="true" />
            {t("downloadErrorReport")}
          </button>
        ) : null}
      </div>

      <div className="overflow-x-auto rounded-lg border border-line">
        <table className="w-full min-w-[720px] text-left text-sm">
          <thead className="bg-ground text-xs uppercase text-ink-muted">
            <tr>
              <th className="px-3 py-2">{t("columnResult")}</th>
              <th className="px-3 py-2">{t("columnLevel")}</th>
              <th className="px-3 py-2">{t("columnCode")}</th>
              <th className="px-3 py-2">{t("columnName")}</th>
              <th className="px-3 py-2">{t("columnParentCode")}</th>
              <th className="px-3 py-2">{t("columnHeadEmail")}</th>
              <th className="px-3 py-2">{t("columnIssue")}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {visibleRows.map((row) => (
              <RowView key={row.rowNumber} row={row} />
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function RowView({ row }: { row: BulkImportRow }) {
  const isError = row.outcome === BULK_IMPORT_OUTCOME.Error;

  return (
    <tr>
      <td className="px-3 py-2">
        <span className={cn("inline-flex rounded-full px-2 py-0.5 text-xs font-medium", OUTCOME_STYLES[row.outcome])}>
          {outcomeLabel(row.outcome)}
        </span>
      </td>
      <td className="px-3 py-2 text-ink-muted">{row.level}</td>
      <td className="px-3 py-2 font-mono text-xs">{row.code}</td>
      <td className="px-3 py-2">{row.name}</td>
      <td className="px-3 py-2 font-mono text-xs text-ink-muted">{row.parentCode}</td>
      <td className="px-3 py-2 text-ink-muted">{row.headEmail || "—"}</td>
      <td className={cn("px-3 py-2", isError ? "text-danger" : "text-ink-muted")}>{row.issue ?? ""}</td>
    </tr>
  );
}

function SummaryCard({
  label,
  count,
  description,
  tone,
}: {
  label: string;
  count: number | string;
  description: string;
  tone?: "warning" | "danger";
}) {
  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{label}</p>
      <p
        className={cn(
          "mt-1 text-2xl font-bold",
          tone === "warning" && "text-status-late-fg",
          tone === "danger" && "text-danger",
          !tone && "text-ink",
        )}
      >
        {count}
      </p>
      <p className="mt-0.5 text-xs text-ink-muted">{description}</p>
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
