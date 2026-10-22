using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Analytics;

/// <summary>HasIndicators: the user has an indicator dashboard (national, or their own Zone/Dila
/// subtree). HasCompliance: the national compliance dashboard (Secretariat, Leadership).</summary>
public sealed record DashboardAccess(bool HasIndicators, bool HasCompliance);

/// <summary>Lets the app shell show a Dashboard link only to people who have one. The dashboards
/// themselves still check access on every request.</summary>
public sealed record GetDashboardAccessQuery(Guid UserId) : IRequest<Result<DashboardAccess>>;

public sealed class GetDashboardAccessQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetDashboardAccessQuery, Result<DashboardAccess>>
{
    public async ValueTask<Result<DashboardAccess>> Handle(GetDashboardAccessQuery request, CancellationToken cancellationToken)
    {
        var units = await DashboardScope.LoadUnitsAsync(db, cancellationToken);
        var scope = await DashboardScope.ResolveAsync(db, accessPolicy, request.UserId, units, clock.UtcNow, cancellationToken);
        return Result.Success(new DashboardAccess(scope.HasAccess, scope.IsNational));
    }
}
