using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Templates.ReportTypes;

public sealed class ArchiveReportTypeCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<ArchiveReportTypeCommand, Result>
{
    public async ValueTask<Result> Handle(ArchiveReportTypeCommand request, CancellationToken cancellationToken)
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

        reportType.Archive(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
