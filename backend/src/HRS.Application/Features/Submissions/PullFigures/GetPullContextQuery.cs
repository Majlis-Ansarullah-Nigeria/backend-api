using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.MyReports;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.PullFigures;

public sealed record GetPullContextQuery(Guid UserId, Guid ReportId, Guid UnitId) : IRequest<Result<PullContextView>>;

public sealed class GetPullContextQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetPullContextQuery, Result<PullContextView>>
{
    private static readonly PullContextView NotEligible = new(false, null, null, [], null);

    public async ValueTask<Result<PullContextView>> Handle(GetPullContextQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.CanEditReportForUnitAsync(request.UserId, request.UnitId, cancellationToken))
        {
            return Result.Failure<PullContextView>(Error.Forbidden("FORBIDDEN", "You don't have access to this unit's reports."));
        }

        var resolved = await PullFiguresResolver.ResolveAsync(db, request.UnitId, request.ReportId, cancellationToken);
        if (resolved is null)
        {
            return Result.Success(NotEligible);
        }

        var now = clock.UtcNow;

        var childUnitIds = resolved.Children.Select(c => c.Id).ToList();
        var childInstances = await db.ReportInstances
            .AsNoTracking()
            .Where(i => i.WindowId == resolved.Window.Id && childUnitIds.Contains(i.UnitId))
            .ToListAsync(cancellationToken);
        var childInstanceByUnit = childInstances.ToDictionary(i => i.UnitId);

        var childOptions = resolved.Children
            .Select(child =>
            {
                childInstanceByUnit.TryGetValue(child.Id, out var childInstance);
                var status = MyReportStatusCalculator.Determine(resolved.Window, childInstance, resolved.ChildLevel.Code, child.Id, now);
                return new PullChildUnitOption(child.Id, child.Name, (int)status);
            })
            .OrderBy(c => c.UnitName, StringComparer.Ordinal)
            .ToList();

        var summary = resolved.Instance?.PullSummaryJson is { } json
            ? JsonSerializer.Deserialize<PullSummary>(json, JsonSerializerOptions.Web)
            : null;

        return Result.Success(new PullContextView(true, resolved.ChildLevel.Name, resolved.ChildLevel.PluralName, childOptions, summary));
    }
}
