namespace HRS.Application.Common.Interfaces;

/// <summary>Renders the submission receipt (FR-SUB-010, a plain, functional document) and
/// FR-HIS-003's branded history download (org name header in place of an actual logo — branding
/// upload is M11.2 and doesn't exist yet — reference number, QR code linking back to the online
/// record).</summary>
public interface IPdfRenderer
{
    byte[] RenderReportReceipt(ReportReceiptData data);

    byte[] RenderHistoryReport(HistoryReportPdfData data);
}

public sealed record ReportReceiptData(
    string ReportTypeName,
    string PeriodLabel,
    string UnitName,
    string? ReferenceNumber,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ReportReceiptSection> Sections);

public sealed record ReportReceiptSection(string Title, IReadOnlyList<ReportReceiptField> Fields);

/// <summary>FieldKey is the schema's stable key, not the (localized, editable) Label — added for
/// M7.3 so the inbox detail view can anchor a new FR-REV-003 comment thread to the right field
/// without guessing from the label text.</summary>
public sealed record ReportReceiptField(string FieldKey, string Label, string Value, bool IsMasked = false);

/// <summary>FR-HIS-003's branded PDF content. OnlineRecordUrl is what the QR code encodes —
/// the renderer only draws it, never computes it (the caller already knows whether this is the
/// submitter's own edit route or an observer's read-only child-report route).</summary>
public sealed record HistoryReportPdfData(
    string ReportTypeName,
    string PeriodLabel,
    string UnitName,
    string? ReferenceNumber,
    DateTimeOffset GeneratedAt,
    string OnlineRecordUrl,
    IReadOnlyList<ReportReceiptSection> Sections);
