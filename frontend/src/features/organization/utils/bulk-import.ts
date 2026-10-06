import type { BulkImportOutcome, BulkImportRow } from "@/lib/api/generated/hRSApiV1.schemas";

// Matches backend/src/HRS.Application/Features/Organization/BulkImport/BulkImportOutcome.cs's
// enum order — the API serializes it as a plain number.
export const BULK_IMPORT_OUTCOME = {
  New: 0,
  Update: 1,
  Warning: 2,
  Error: 3,
} as const;

export type BulkImportTab = "all" | "errors" | "warnings" | "updates";

export function filterRowsByTab(rows: BulkImportRow[], tab: BulkImportTab): BulkImportRow[] {
  switch (tab) {
    case "errors":
      return rows.filter((r) => r.outcome === BULK_IMPORT_OUTCOME.Error);
    case "warnings":
      return rows.filter((r) => r.outcome === BULK_IMPORT_OUTCOME.Warning);
    case "updates":
      return rows.filter((r) => r.outcome === BULK_IMPORT_OUTCOME.Update);
    default:
      return rows;
  }
}

export function outcomeLabel(outcome: BulkImportOutcome): string {
  switch (outcome) {
    case BULK_IMPORT_OUTCOME.New:
      return "Ready";
    case BULK_IMPORT_OUTCOME.Update:
      return "Update";
    case BULK_IMPORT_OUTCOME.Warning:
      return "Warning";
    case BULK_IMPORT_OUTCOME.Error:
      return "Error";
    default:
      return "Unknown";
  }
}

function csvEscape(value: unknown): string {
  const str = String(value ?? "");
  return /[",\n]/.test(str) ? `"${str.replace(/"/g, '""')}"` : str;
}

/** Errors-only CSV for the "Download error report" link — built client-side from the already-fetched preview. */
export function errorReportCsv(rows: BulkImportRow[]): string {
  const header = ["Row", "Result", "Level", "Code", "Name", "Parent Code", "Head Email", "Issue"];
  const lines = rows
    .filter((r) => r.outcome === BULK_IMPORT_OUTCOME.Error)
    .map((r) =>
      [r.rowNumber, outcomeLabel(r.outcome), r.level, r.code, r.name, r.parentCode, r.headEmail, r.issue ?? ""]
        .map(csvEscape)
        .join(","),
    );

  return [header.join(","), ...lines].join("\n");
}

export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}
