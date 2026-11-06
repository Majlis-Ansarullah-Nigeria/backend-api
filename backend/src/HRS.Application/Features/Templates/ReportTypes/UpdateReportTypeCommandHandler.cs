using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Templates.ReportTypes;

public sealed class UpdateReportTypeCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<UpdateReportTypeCommand, Result<ReportTypeSummary>>
{
    public async ValueTask<Result<ReportTypeSummary>> Handle(UpdateReportTypeCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsTemplateAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<ReportTypeSummary>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage report types."));
        }

        var reportType = await db.ReportTypes.FirstOrDefaultAsync(r => r.Id == request.ReportTypeId, cancellationToken);

        if (reportType is null)
        {
            return Result.Failure<ReportTypeSummary>(Error.NotFound("REPORT_TYPE_NOT_FOUND", "Report type not found."));
        }

        if (!string.IsNullOrWhiteSpace(request.Code) && !string.Equals(request.Code.Trim(), reportType.Code, StringComparison.OrdinalIgnoreCase))
        {
            if (await ReportTypeCodes.IsTakenAsync(db, request.Code, reportType.Id, cancellationToken))
            {
                return Result.Failure<ReportTypeSummary>(ReportTypeCodes.Taken);
            }

            // References already issued keep one format: the code is fixed once any report of
            // this type has been submitted.
            if (await ReportTypeCodes.HasIssuedReferencesAsync(db, reportType.Id, cancellationToken))
            {
                return Result.Failure<ReportTypeSummary>(ReportTypeCodes.Locked);
            }

            reportType.ChangeCode(request.Code);
        }

        reportType.Update(
            request.Name,
            request.Description,
            request.Category,
            request.Frequency,
            request.ApplicableLevels,
            request.RecipientLevel,
            request.ObserverDepth,
            request.AllowAttachments,
            request.AllowNilReport);

        await db.SaveChangesAsync(cancellationToken);

        return ReportTypeSummary.FromEntity(reportType);
    }
}
