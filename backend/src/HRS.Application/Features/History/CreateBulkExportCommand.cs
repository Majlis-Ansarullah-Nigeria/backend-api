using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Common;
using HRS.Domain.History;
using Mediator;

namespace HRS.Application.Features.History;

/// <summary>FR-HIS-004 from /history: the caller already picked specific rows from their own
/// scoped list. The background job re-validates each one anyway (HistoryReportResolver) — never
/// trusting a client-submitted id list blindly.</summary>
public sealed record CreateBulkExportCommand(Guid UserId, IReadOnlyList<Guid> InstanceIds, BulkExportFormat Format)
    : IRequest<Result<ExportSummary>>;

public sealed class CreateBulkExportCommandHandler(IAppDbContext db, IClock clock, IBackgroundJobScheduler jobScheduler, IAuditLog auditLog)
    : IRequestHandler<CreateBulkExportCommand, Result<ExportSummary>>
{
    public async ValueTask<Result<ExportSummary>> Handle(CreateBulkExportCommand request, CancellationToken cancellationToken)
    {
        if (request.InstanceIds.Count == 0)
        {
            return Result.Failure<ExportSummary>(Error.Validation("VALIDATION", "Select at least one report to export."));
        }

        var scopeJson = JsonSerializer.Serialize(new InstanceIdsScope(request.InstanceIds));
        var export = BulkExport.Create(
            Guid.CreateVersion7(), request.UserId, request.Format, BulkExportScopeKind.InstanceIds, scopeJson, clock.UtcNow);
        db.BulkExports.Add(export);
        auditLog.Add(new AuditEntry(
            AuditActions.ExportRequested, request.UserId, "export", export.Id.ToString(),
            $"{request.Format} of {request.InstanceIds.Count} report(s)", After: new { Format = request.Format.ToString(), Reports = request.InstanceIds.Count }));
        await db.SaveChangesAsync(cancellationToken);

        jobScheduler.EnqueueBulkExport(export.Id);

        return Result.Success(ExportSummary.From(export));
    }
}
