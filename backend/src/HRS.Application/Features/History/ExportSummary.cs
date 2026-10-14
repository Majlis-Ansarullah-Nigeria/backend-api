using HRS.Domain.History;

namespace HRS.Application.Features.History;

public sealed record ExportSummary(
    Guid Id,
    Guid RequestedByUserId,
    string RequestedByName,
    BulkExportFormat Format,
    BulkExportStatus Status,
    int ReportCount,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static ExportSummary From(BulkExport export, string requestedByName = "") => new(
        export.Id, export.RequestedByUserId, requestedByName, export.Format, export.Status,
        export.ReportCount, export.ErrorMessage, export.CreatedAt, export.CompletedAt);
}
