using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Organization.Levels;

/// <summary>
/// The fixed-but-configurable level vocabulary (hrs-domain skill), for pickers like the
/// "Add unit" form — the frontend never hardcodes the seeded level GUIDs.
/// </summary>
public sealed record GetUnitLevelsQuery : IRequest<Result<IReadOnlyList<UnitLevelSummary>>>;

public sealed class GetUnitLevelsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetUnitLevelsQuery, Result<IReadOnlyList<UnitLevelSummary>>>
{
    public async ValueTask<Result<IReadOnlyList<UnitLevelSummary>>> Handle(
        GetUnitLevelsQuery request, CancellationToken cancellationToken)
    {
        var levels = await db.UnitLevels.AsNoTracking().OrderBy(l => l.Depth).ToListAsync(cancellationToken);

        IReadOnlyList<UnitLevelSummary> summaries = levels.Select(UnitLevelSummary.FromEntity).ToList();

        return Result.Success(summaries);
    }
}
