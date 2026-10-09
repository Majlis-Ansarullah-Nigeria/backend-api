namespace HRS.Application.Common.Interfaces;

/// <summary>Renders the submission receipt (FR-SUB-010) — a plain, functional document, not the
/// branded/QR-code version FR-HIS-003 (M9) builds later for history downloads.</summary>
public interface IPdfRenderer
{
    byte[] RenderReportReceipt(ReportReceiptData data);
}

public sealed record ReportReceiptData(
    string ReportTypeName,
    string PeriodLabel,
    string UnitName,
    string? ReferenceNumber,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ReportReceiptSection> Sections);

public sealed record ReportReceiptSection(string Title, IReadOnlyList<ReportReceiptField> Fields);

public sealed record ReportReceiptField(string Label, string Value);
