namespace HRS.Application.Common.Interfaces;

/// <summary>FR-HIS-003's single-report Excel download and FR-HIS-004's bulk workbook (one row per
/// report, one column per field — different report types have different fields entirely, so the
/// bulk workbook's columns are the union of every exported report's field keys, blank where a
/// given report's own schema doesn't have that field).</summary>
public interface IExcelRenderer
{
    byte[] RenderSingleReport(ReportReceiptData data, OrganizationBranding branding);

    byte[] RenderBulkWorkbook(IReadOnlyList<BulkExportReportRow> rows, OrganizationBranding branding);
}

/// <summary>One row of the bulk workbook. AnswersByFieldKey uses the schema's stable field key as
/// the column identifier — the workbook's header row resolves each key to its (first-seen) label.</summary>
public sealed record BulkExportReportRow(
    string ReportTypeName,
    string UnitName,
    string PeriodLabel,
    string? ReferenceNumber,
    DateTimeOffset? SubmittedAt,
    string Status,
    IReadOnlyDictionary<string, (string Label, string Value)> AnswersByFieldKey);
