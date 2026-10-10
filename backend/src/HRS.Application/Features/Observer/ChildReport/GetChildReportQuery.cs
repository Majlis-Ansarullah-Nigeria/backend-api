using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.Review;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Observer.ChildReport;

public sealed record GetChildReportQuery(Guid UserId, Guid ChildUnitId, Guid ReportId) : IRequest<Result<ChildReportView>>;

public sealed class GetChildReportQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService, IClock clock)
    : IRequestHandler<GetChildReportQuery, Result<ChildReportView>>
{
    public async ValueTask<Result<ChildReportView>> Handle(GetChildReportQuery request, CancellationToken cancellationToken)
    {
        var resolved = await ChildReportResolver.ResolveAsync(
            db, accessPolicy, request.UserId, request.ChildUnitId, request.ReportId, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<ChildReportView>(resolved.Error);
        }

        var (_, childUnitName, childLevelCode, instance, window, reportType, schema) = resolved.Value;

        var receipt = await ReportReceiptBuilder.BuildAsync(
            db, instance, schema, reportType.Name, window.PeriodLabel, childUnitName, (int)childLevelCode,
            instance.ReferenceNumber, instance.SubmittedAt ?? clock.UtcNow, cancellationToken, hideConfidentialValues: true);

        string? submittedByName = null;
        if (instance.SubmittedByUserId is { } submitterId)
        {
            var names = await identityService.GetUserNamesAsync([submitterId], cancellationToken);
            names.TryGetValue(submitterId, out submittedByName);
        }

        return Result.Success(new ChildReportView(
            instance.Id, (int)instance.Status, instance.IsLate, instance.SubmittedAt, submittedByName, receipt));
    }
}
