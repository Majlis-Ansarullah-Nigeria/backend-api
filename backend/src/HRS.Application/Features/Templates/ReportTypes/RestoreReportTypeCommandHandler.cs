using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Templates.ReportTypes;

public sealed class RestoreReportTypeCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<RestoreReportTypeCommand, Result>
{
    public async ValueTask<Result> Handle(RestoreReportTypeCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsTemplateAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage report types."));
        }

        var reportType = await db.ReportTypes.FirstOrDefaultAsync(r => r.Id == request.ReportTypeId, cancellationToken);

        if (reportType is null)
        {
            return Result.Failure(Error.NotFound("REPORT_TYPE_NOT_FOUND", "Report type not found."));
        }

        reportType.Restore();
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
