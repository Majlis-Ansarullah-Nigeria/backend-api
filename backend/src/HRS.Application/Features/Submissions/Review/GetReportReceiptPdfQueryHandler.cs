using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.Review;

public sealed class GetReportReceiptPdfQueryHandler(
    IAppDbContext db, IAccessPolicy accessPolicy, IPdfRenderer pdfRenderer, IClock clock, IOrganizationBrandingProvider brandingProvider)
    : IRequestHandler<GetReportReceiptPdfQuery, Result<byte[]>>
{
    public async ValueTask<Result<byte[]>> Handle(GetReportReceiptPdfQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.CanEditReportForUnitAsync(request.UserId, request.UnitId, cancellationToken))
        {
            return Result.Failure<byte[]>(Error.Forbidden("FORBIDDEN", "You don't have access to this unit's reports."));
        }

        var instance = await db.ReportInstances.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.ReportId && i.UnitId == request.UnitId, cancellationToken);
        instance ??= await db.ReportInstances.AsNoTracking()
            .FirstOrDefaultAsync(i => i.WindowId == request.ReportId && i.UnitId == request.UnitId, cancellationToken);

        if (instance is null)
        {
            return Result.Failure<byte[]>(Error.NotFound("REPORT_NOT_FOUND", "Nothing has been saved yet."));
        }

        var window = await db.SubmissionWindows.AsNoTracking().FirstAsync(w => w.Id == instance.WindowId, cancellationToken);
        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        var unitRow = await db.Units.AsNoTracking()
            .Where(u => u.Id == request.UnitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Name, LevelCode = l.Code })
            .FirstAsync(cancellationToken);
        var templateVersion = await db.TemplateVersions.AsNoTracking().FirstAsync(v => v.Id == instance.TemplateVersionId, cancellationToken);
        var schema = JsonSerializer.Deserialize<TemplateSchema>(templateVersion.SchemaJson, JsonSerializerOptions.Web)!;

        var generatedAt = instance.SubmittedAt ?? clock.UtcNow;
        var receiptData = await ReportReceiptBuilder.BuildAsync(
            db, instance, schema, reportType.Name, window.PeriodLabel, unitRow.Name, (int)unitRow.LevelCode,
            instance.ReferenceNumber, generatedAt, cancellationToken);

        return pdfRenderer.RenderReportReceipt(receiptData, await brandingProvider.GetAsync(cancellationToken, includeLogo: true));
    }
}
