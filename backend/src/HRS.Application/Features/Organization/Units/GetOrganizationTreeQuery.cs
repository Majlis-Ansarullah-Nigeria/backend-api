using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Organization.Units;

/// <summary>
/// The whole organization in one shot (a few hundred units at most), flat with parent
/// pointers — the client builds the tree, search and expand/collapse client-side (FR-ORG-002).
/// </summary>
public sealed record GetOrganizationTreeQuery(bool IncludeArchived) : IRequest<Result<IReadOnlyList<UnitTreeNode>>>;

public sealed class GetOrganizationTreeQueryHandler(IAppDbContext db, IIdentityService identityService, IClock clock)
    : IRequestHandler<GetOrganizationTreeQuery, Result<IReadOnlyList<UnitTreeNode>>>
{
    public async ValueTask<Result<IReadOnlyList<UnitTreeNode>>> Handle(
        GetOrganizationTreeQuery request, CancellationToken cancellationToken)
    {
        var query = db.Units.AsNoTracking().AsQueryable();

        if (!request.IncludeArchived)
        {
            query = query.Where(u => u.ArchivedAt == null);
        }

        // Join and materialize the raw rows in SQL; UnitTreeNode.FromEntity (which calls
        // DisplayCode's string-split logic) can't be translated, so it runs after, in memory.
        var rows = await query
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { Unit = u, l.Code })
            .OrderBy(r => r.Unit.Path)
            .ToListAsync(cancellationToken);

        var now = clock.UtcNow;

        // Inline the active-window check rather than calling RoleAssignment.IsActive() — that
        // method isn't translatable by EF, same lesson as GetUnitsQuery's DisplayCode() fix.
        var activeHeadAssignments = await db.RoleAssignments
            .AsNoTracking()
            .Where(a => a.Role == RoleName.UnitHead && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .OrderByDescending(a => a.StartsAt)
            .Select(a => new { a.UnitId, a.UserId })
            .ToListAsync(cancellationToken);

        // Most-recently-started assignment wins if more than one is somehow active for a unit.
        var headUserIdByUnit = activeHeadAssignments
            .GroupBy(a => a.UnitId)
            .ToDictionary(g => g.Key, g => g.First().UserId);

        var headNames = await identityService.GetUserNamesAsync(
            headUserIdByUnit.Values.Distinct().ToList(), cancellationToken);

        IReadOnlyList<UnitTreeNode> nodes = rows
            .Select(r =>
            {
                var headName = headUserIdByUnit.TryGetValue(r.Unit.Id, out var headUserId)
                    && headNames.TryGetValue(headUserId, out var name)
                    ? name
                    : null;

                return UnitTreeNode.FromEntity(r.Unit, r.Code, headName);
            })
            .ToList();

        return Result.Success(nodes);
    }
}
