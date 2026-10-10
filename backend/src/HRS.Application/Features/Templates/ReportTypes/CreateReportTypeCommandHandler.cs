using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Templates;
using Mediator;

namespace HRS.Application.Features.Templates.ReportTypes;

public sealed class CreateReportTypeCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<CreateReportTypeCommand, Result<ReportTypeSummary>>
{
    public async ValueTask<Result<ReportTypeSummary>> Handle(CreateReportTypeCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsTemplateAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<ReportTypeSummary>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage report types."));
        }

        var reportType = ReportType.Create(
            Guid.CreateVersion7(),
            request.Name,
            request.Description,
            request.Category,
            request.Frequency,
            request.ApplicableLevels,
            request.RecipientLevel,
            request.ObserverDepth,
            request.AllowAttachments,
            request.AllowNilReport,
            clock.UtcNow);

        db.ReportTypes.Add(reportType);
        await db.SaveChangesAsync(cancellationToken);

        return ReportTypeSummary.FromEntity(reportType);
    }
}
