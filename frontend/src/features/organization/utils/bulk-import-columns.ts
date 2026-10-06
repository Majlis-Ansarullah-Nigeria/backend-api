/**
 * Mirrors backend/src/HRS.Application/Features/Organization/BulkImport/BulkImportColumns.cs —
 * display-only here (the "Match columns" step is a confirmation, not a real column mapper;
 * the backend is what actually validates headers).
 */
export const BulkImportColumns = [
  "Level",
  "Code",
  "Name",
  "Parent Code",
  "Address",
  "Contact Phone",
  "Contact Email",
  "Head Name",
  "Head Email",
] as const;
