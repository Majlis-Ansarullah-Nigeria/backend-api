using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.Review;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.History;

/// <summary>FR-HIS-003's single-report Excel download.</summary>
public sealed record GetHistoryReportExcelQuery(Guid UserId, Guid InstanceId) : IRequest<Result<byte[]>>;

public sealed class GetHistoryReportExcelQueryHandler(IAppDbContext db, IExcelRenderer excelRenderer, IClock clock)
    : IRequestHandler<GetHistoryReportExcelQuery, Result<byte[]>>
{
    public async ValueTask<Result<byte[]>> Handle(GetHistoryReportExcelQuery request, CancellationToken cancellationToken)
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

        return excelRenderer.RenderSingleReport(receipt);
    }
}
