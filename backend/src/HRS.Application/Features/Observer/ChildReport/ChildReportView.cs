using HRS.Application.Common.Interfaces;

namespace HRS.Application.Features.Observer.ChildReport;

/// <summary>FR-OBS-002's read-only child report. Status/IsLate/SubmittedAt/SubmittedByName sit
/// alongside the shared receipt content (Receipt) rather than duplicating it — the frontend
/// renders the header chips from these and the sections straight from Receipt, same split as the
/// submitted/receipt screens already use for ReportReceiptData.</summary>
public sealed record ChildReportView(
    Guid ReportId,
    int Status,
    bool IsLate,
    DateTimeOffset? SubmittedAt,
    string? SubmittedByName,
    ReportReceiptData Receipt);
