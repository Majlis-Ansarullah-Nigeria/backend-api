using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Audit;

public enum DownloadKind
{
    Report,
    Export,
    CompiledReport,
    AuditLog,
}

/// <summary>
/// FR-AUD-001's "exports and downloads". Download handlers are queries and never change state, so
/// the endpoint sends this after a successful download to record it. For a report, EntityId is
/// the report instance — or, as some routes take it, the window, with UnitId saying whose report.
/// </summary>
public sealed record RecordDownloadCommand(Guid UserId, DownloadKind Kind, Guid EntityId, string Format, Guid? UnitId = null) : IRequest<Result>;

public sealed class RecordDownloadCommandHandler(IAppDbContext db, IAuditLog auditLog) : IRequestHandler<RecordDownloadCommand, Result>
{
    public async ValueTask<Result> Handle(RecordDownloadCommand request, CancellationToken cancellationToken)
    {
        var entry = request.Kind switch
        {
            DownloadKind.Report => await ReportEntryAsync(request, cancellationToken),
            DownloadKind.Export => await ExportEntryAsync(request, cancellationToken),
            DownloadKind.AuditLog => new AuditEntry(
                AuditActions.AuditExported, request.UserId, "audit_log", null, $"Exported the audit log as CSV ({request.Format})"),
            _ => new AuditEntry(
                AuditActions.CompiledReportDownloaded, request.UserId, "compiled_report", request.EntityId.ToString(),
                $"{await AuditDescriptions.WindowAsync(db, request.EntityId, cancellationToken)} · {request.Format}"),
        };

        await auditLog.RecordAsync(entry, cancellationToken);
        return Result.Success();
    }

    private async Task<AuditEntry> ReportEntryAsync(RecordDownloadCommand request, CancellationToken cancellationToken)
    {
        var instance = await db.ReportInstances.AsNoTracking()
            .Where(i => i.Id == request.EntityId || (i.WindowId == request.EntityId && i.UnitId == request.UnitId))
            .Select(i => new { i.Id, i.WindowId, i.UnitId })
            .FirstOrDefaultAsync(cancellationToken);
        var description = instance is null
            ? request.EntityId.ToString()
            : await AuditDescriptions.ReportAsync(db, instance.WindowId, instance.UnitId, cancellationToken);
        return new AuditEntry(
            AuditActions.ReportDownloaded, request.UserId, "report", (instance?.Id ?? request.EntityId).ToString(),
            $"{description} · {request.Format}", instance?.UnitId ?? request.UnitId);
    }

    private async Task<AuditEntry> ExportEntryAsync(RecordDownloadCommand request, CancellationToken cancellationToken)
    {
        var format = await db.BulkExports.AsNoTracking()
            .Where(e => e.Id == request.EntityId).Select(e => (Domain.History.BulkExportFormat?)e.Format).FirstOrDefaultAsync(cancellationToken);
        return new AuditEntry(
            AuditActions.ExportDownloaded, request.UserId, "export", request.EntityId.ToString(),
            $"{format?.ToString() ?? request.Format} export downloaded");
    }
}
