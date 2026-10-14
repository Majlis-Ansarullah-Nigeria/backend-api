import type { StatusChipVariant } from "@/components/shared/StatusChip";

// Matches backend/src/HRS.Domain/History/BulkExportFormat.cs's enum order exactly — the API
// serializes it as a plain number.
export const BULK_EXPORT_FORMAT = {
  ZipOfPdfs: 0,
  ExcelWorkbook: 1,
} as const;

// Matches backend/src/HRS.Domain/History/BulkExportStatus.cs's enum order exactly — the API
// serializes it as a plain number.
export const BULK_EXPORT_STATUS = {
  Pending: 0,
  Processing: 1,
  Ready: 2,
  Failed: 3,
} as const;

const STATUS_CHIP_VARIANTS: Record<number, StatusChipVariant> = {
  [BULK_EXPORT_STATUS.Pending]: "not-started",
  [BULK_EXPORT_STATUS.Processing]: "draft",
  [BULK_EXPORT_STATUS.Ready]: "acknowledged",
  [BULK_EXPORT_STATUS.Failed]: "missed",
};

export function exportStatusChipVariant(status: number): StatusChipVariant {
  return STATUS_CHIP_VARIANTS[status] ?? "not-started";
}
