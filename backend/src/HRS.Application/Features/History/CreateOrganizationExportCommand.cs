using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.History;
using Mediator;

namespace HRS.Application.Features.History;

/// <summary>FR-HIS-004 from /secretariat/exports: Secretariat-only, organization-wide — no
/// observer/head scope restriction, since Secretariat can act on everything (routing table).
/// CSV and a Units multi-select from the mockup aren't built: FR-HIS-004's own text only asks for
/// a ZIP of PDFs or one Excel workbook, filterable enough by report type/period/status.</summary>
public sealed record CreateOrganizationExportCommand(
    Guid UserId, Guid? ReportTypeId, Guid? FromWindowId, Guid? ToWindowId, int? Status, BulkExportFormat Format)
    : IRequest<Result<ExportSummary>>;

public sealed class CreateOrganizationExportCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IBackgroundJobScheduler jobScheduler, IAuditLog auditLog)
    : IRequestHandler<CreateOrganizationExportCommand, Result<ExportSummary>>
{
    public async ValueTask<Result<ExportSummary>> Handle(CreateOrganizationExportCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<ExportSummary>(Error.Forbidden("FORBIDDEN", "Only the Secretariat can run organization-wide exports."));
        }

        var scopeJson = JsonSerializer.Serialize(new OrganizationFilterScope(request.ReportTypeId, request.FromWindowId, request.ToWindowId, request.Status));
        var export = BulkExport.Create(
            Guid.CreateVersion7(), request.UserId, request.Format, BulkExportScopeKind.OrganizationFilter, scopeJson, clock.UtcNow);
        db.BulkExports.Add(export);
        auditLog.Add(new AuditEntry(
            AuditActions.ExportRequested, request.UserId, "export", export.Id.ToString(),
            $"{request.Format} export for the organization", After: new { Format = request.Format.ToString(), Scope = scopeJson }));
        await db.SaveChangesAsync(cancellationToken);

        jobScheduler.EnqueueBulkExport(export.Id);

        return Result.Success(ExportSummary.From(export));
    }
}
