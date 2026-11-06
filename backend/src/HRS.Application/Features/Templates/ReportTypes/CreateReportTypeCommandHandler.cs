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

        // FR-SUB-010: the type's own reference code — the Secretariat's choice, or the next free
        // one for its category.
        string code;
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            var suggested = await ReportTypeCodes.SuggestAsync(db, request.Category, cancellationToken);
            if (suggested is null)
            {
                return Result.Failure<ReportTypeSummary>(ReportTypeCodes.NoneFree);
            }

            code = suggested;
        }
        else if (await ReportTypeCodes.IsTakenAsync(db, request.Code, null, cancellationToken))
        {
            return Result.Failure<ReportTypeSummary>(ReportTypeCodes.Taken);
        }
        else
        {
            code = request.Code;
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
            code,
            clock.UtcNow);

        db.ReportTypes.Add(reportType);
        await db.SaveChangesAsync(cancellationToken);

        return ReportTypeSummary.FromEntity(reportType);
    }
}
