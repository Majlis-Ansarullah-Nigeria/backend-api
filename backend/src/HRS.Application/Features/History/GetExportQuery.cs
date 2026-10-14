using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.History;

/// <summary>Status polling for one export (the "export is ready" notification's own link lands
/// here). Visible to whoever requested it, or any Secretariat staff member (the shared "Recent
/// exports" workspace).</summary>
public sealed record GetExportQuery(Guid UserId, Guid ExportId) : IRequest<Result<ExportSummary>>;

public sealed class GetExportQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService)
    : IRequestHandler<GetExportQuery, Result<ExportSummary>>
{
    public async ValueTask<Result<ExportSummary>> Handle(GetExportQuery request, CancellationToken cancellationToken)
    {
        var export = await db.BulkExports.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.ExportId, cancellationToken);
        if (export is null)
        {
            return Result.Failure<ExportSummary>(Error.NotFound("EXPORT_NOT_FOUND", "That export doesn't exist."));
        }

        var isOwner = export.RequestedByUserId == request.UserId;
        if (!isOwner && !await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<ExportSummary>(Error.Forbidden("FORBIDDEN", "You don't have access to that export."));
        }

        var requester = await identityService.GetUserAsync(export.RequestedByUserId, cancellationToken);
        return Result.Success(ExportSummary.From(export, requester?.Name ?? "Unknown"));
    }
}
