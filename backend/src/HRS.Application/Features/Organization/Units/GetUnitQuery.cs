using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Organization.Units;

public sealed record GetUnitQuery(Guid UnitId) : IRequest<Result<UnitSummary>>;

public sealed class GetUnitQueryHandler(IAppDbContext db) : IRequestHandler<GetUnitQuery, Result<UnitSummary>>
{
    public async ValueTask<Result<UnitSummary>> Handle(GetUnitQuery request, CancellationToken cancellationToken)
    {
        // Join and materialize the raw rows in SQL; UnitSummary.FromEntity (which calls
        // DisplayCode's string-split logic) can't be translated, so it runs after, in memory.
        var row = await db.Units.AsNoTracking()
            .Where(u => u.Id == request.UnitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { Unit = u, l.Code })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? Result.Failure<UnitSummary>(Error.NotFound("UNIT_NOT_FOUND", "Unit not found."))
            : UnitSummary.FromEntity(row.Unit, row.Code);
    }
}
