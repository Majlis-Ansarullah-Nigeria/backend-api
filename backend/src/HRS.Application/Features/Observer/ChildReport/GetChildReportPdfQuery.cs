using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.Review;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Observer.ChildReport;

public sealed record GetChildReportPdfQuery(Guid UserId, Guid ChildUnitId, Guid ReportId) : IRequest<Result<byte[]>>;

public sealed class GetChildReportPdfQueryHandler(
    IAppDbContext db, IAccessPolicy accessPolicy, IPdfRenderer pdfRenderer, IClock clock, IOrganizationBrandingProvider brandingProvider)
    : IRequestHandler<GetChildReportPdfQuery, Result<byte[]>>
{
    public async ValueTask<Result<byte[]>> Handle(GetChildReportPdfQuery request, CancellationToken cancellationToken)
    {
        var resolved = await ChildReportResolver.ResolveAsync(
            db, accessPolicy, request.UserId, request.ChildUnitId, request.ReportId, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<byte[]>(resolved.Error);
        }

        var (_, childUnitName, childLevelCode, instance, window, reportType, schema, _) = resolved.Value;

        var receipt = await ReportReceiptBuilder.BuildAsync(
            db, instance, schema, reportType.Name, window.PeriodLabel, childUnitName, (int)childLevelCode,
            instance.ReferenceNumber, instance.SubmittedAt ?? clock.UtcNow, cancellationToken, hideConfidentialValues: true);

        return pdfRenderer.RenderReportReceipt(receipt, await brandingProvider.GetAsync(cancellationToken, includeLogo: true));
    }
}
