using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Options;
using HRS.Application.Features.Submissions.Review;
using HRS.Domain.Common;
using Mediator;
using Microsoft.Extensions.Options;

namespace HRS.Application.Features.History;

/// <summary>FR-HIS-003's branded single-report PDF download.</summary>
public sealed record GetHistoryReportPdfQuery(Guid UserId, Guid InstanceId) : IRequest<Result<byte[]>>;

public sealed class GetHistoryReportPdfQueryHandler(
    IAppDbContext db, IPdfRenderer pdfRenderer, IClock clock, IOptions<AppOptions> appOptions, IOrganizationBrandingProvider brandingProvider)
    : IRequestHandler<GetHistoryReportPdfQuery, Result<byte[]>>
{
    public async ValueTask<Result<byte[]>> Handle(GetHistoryReportPdfQuery request, CancellationToken cancellationToken)
    {
        var resolved = await HistoryReportResolver.ResolveAsync(db, request.UserId, request.InstanceId, clock, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<byte[]>(resolved.Error);
        }

        var r = resolved.Value;
        var receipt = await ReportReceiptBuilder.BuildAsync(
            db, r.Instance, r.Schema, r.ReportType.Name, r.Window.PeriodLabel, r.UnitName, (int)r.UnitLevelCode,
            r.Instance.ReferenceNumber, r.Instance.SubmittedAt ?? clock.UtcNow, cancellationToken, hideConfidentialValues: r.IsObserved);

        var onlineRecordUrl = HistoryOnlineRecordUrl.Build(appOptions.Value.PublicUrl, r.UnitId, r.Instance.Id, r.IsObserved);

        var pdfData = new HistoryReportPdfData(
            receipt.ReportTypeName, receipt.PeriodLabel, receipt.UnitName, receipt.ReferenceNumber, receipt.GeneratedAt,
            onlineRecordUrl, receipt.Sections);

        return pdfRenderer.RenderHistoryReport(pdfData, await brandingProvider.GetAsync(cancellationToken, includeLogo: true));
    }
}
