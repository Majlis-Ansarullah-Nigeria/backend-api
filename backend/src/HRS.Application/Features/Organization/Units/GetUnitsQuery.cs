using System.Diagnostics.CodeAnalysis;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Organization.Units;

/// <summary>
/// A flat, filtered list — not the tree-shaped response with vacant-head badges (that's
/// GET /units/tree, M2.2).
/// </summary>
public sealed record GetUnitsQuery(
    Guid? ParentId,
    Guid? LevelId,
    string? Search,
    bool IncludeArchived) : IRequest<Result<IReadOnlyList<UnitSummary>>>;

public sealed class GetUnitsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetUnitsQuery, Result<IReadOnlyList<UnitSummary>>>
{
    [SuppressMessage(
        "Globalization",
        "CA1304:Specify CultureInfo",
        Justification = "This .ToLower() is inside a LINQ expression tree translated to SQL (Postgres LOWER()), " +
            "never executed by .NET's culture-aware runtime — the overload with a CultureInfo argument isn't " +
            "translatable, and EF.Functions.ILike would leak a Postgres-specific function into Application.")]
    [SuppressMessage(
        "Globalization",
        "CA1311:Specify a culture or use an invariant version",
        Justification = "Same as CA1304 above — this runs as translated SQL, not .NET string comparison.")]
    [SuppressMessage(
        "Performance",
        "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons",
        Justification = "The StringComparison overload of Contains isn't translatable by the Npgsql EF provider; " +
            ".ToLower().Contains() is the standard translatable pattern for a case-insensitive LIKE.")]
    public async ValueTask<Result<IReadOnlyList<UnitSummary>>> Handle(
        GetUnitsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Units.AsNoTracking().AsQueryable();

        if (request.ParentId is { } parentId)
        {
            query = query.Where(u => u.ParentId == parentId);
        }

        if (request.LevelId is { } levelId)
        {
            query = query.Where(u => u.LevelId == levelId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // .ToLower() + .Contains() (not EF.Functions.ILike) so this stays provider-agnostic —
            // Application shouldn't need to know the database is Postgres specifically.
            var needle = request.Search.ToLowerInvariant();
            query = query.Where(u => u.Name.ToLower().Contains(needle));
        }

        if (!request.IncludeArchived)
        {
            query = query.Where(u => u.ArchivedAt == null);
        }

        // Join and materialize the raw rows in SQL; UnitSummary.FromEntity (which calls
        // DisplayCode's string-split logic) can't be translated, so it runs after, in memory.
        var rows = await query
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { Unit = u, l.Code })
            .OrderBy(r => r.Unit.Path)
            .ToListAsync(cancellationToken);

        IReadOnlyList<UnitSummary> summaries = rows.Select(r => UnitSummary.FromEntity(r.Unit, r.Code)).ToList();

        return Result.Success(summaries);
    }
}
