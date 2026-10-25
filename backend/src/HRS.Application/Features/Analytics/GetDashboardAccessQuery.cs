using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Analytics;

/// <summary>HasIndicators: the user has an indicator dashboard (national, or their own Zone/Dila
/// subtree). HasCompliance: the national compliance dashboard (Secretariat, Leadership).
/// HasCompiledReports: the compiled national reports (FR-REV-006 — Secretariat, Leadership).</summary>
public sealed record DashboardAccess(bool HasIndicators, bool HasCompliance, bool HasCompiledReports);

/// <summary>Lets the app shell show a Dashboard link only to people who have one. The dashboards
/// themselves still check access on every request.</summary>
public sealed record GetDashboardAccessQuery(Guid UserId) : IRequest<Result<DashboardAccess>>;

public sealed class GetDashboardAccessQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IFeatureFlags featureFlags)
    : IRequestHandler<GetDashboardAccessQuery, Result<DashboardAccess>>
{
    public async ValueTask<Result<DashboardAccess>> Handle(GetDashboardAccessQuery request, CancellationToken cancellationToken)
    {
        var units = await DashboardScope.LoadUnitsAsync(db, cancellationToken);
        var scope = await DashboardScope.ResolveAsync(db, accessPolicy, request.UserId, units, clock.UtcNow, cancellationToken);
        // FR-ADM-004: a switched-off module isn't offered to anyone.
        var dashboards = await featureFlags.IsEnabledAsync(Domain.Admin.FeatureModule.Dashboards, cancellationToken);
        var compiled = await featureFlags.IsEnabledAsync(Domain.Admin.FeatureModule.CompiledReports, cancellationToken);
        return Result.Success(new DashboardAccess(dashboards && scope.HasAccess, dashboards && scope.IsNational, compiled && scope.IsNational));
    }
}
