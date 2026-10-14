using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.History;

/// <summary>"Recent exports" — the caller's own, or every Secretariat staff member's for the
/// Secretariat (the mockup's shared team workspace, "By Y. Danjuma"-style attribution).</summary>
public sealed record GetMyExportsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<ExportSummary>>>;

public sealed class GetMyExportsQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService)
    : IRequestHandler<GetMyExportsQuery, Result<IReadOnlyList<ExportSummary>>>
{
    private const int MaxResults = 50;

    public async ValueTask<Result<IReadOnlyList<ExportSummary>>> Handle(GetMyExportsQuery request, CancellationToken cancellationToken)
    {
        var isSecretariat = await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken);

        var exports = await db.BulkExports.AsNoTracking()
            .Where(e => isSecretariat || e.RequestedByUserId == request.UserId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(MaxResults)
            .ToListAsync(cancellationToken);

        var requesterIds = exports.Select(e => e.RequestedByUserId).Distinct().ToList();
        var namesByUserId = await identityService.GetUserNamesAsync(requesterIds, cancellationToken);

        IReadOnlyList<ExportSummary> summaries = exports
            .Select(e => ExportSummary.From(e, namesByUserId.GetValueOrDefault(e.RequestedByUserId, "Unknown")))
            .ToList();

        return Result.Success(summaries);
    }
}
