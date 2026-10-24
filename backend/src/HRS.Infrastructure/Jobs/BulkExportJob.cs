using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Options;
using HRS.Application.Features.History;
using Microsoft.Extensions.Options;

namespace HRS.Infrastructure.Jobs;

/// <summary>
/// FR-HIS-004's Hangfire-invokable unit of work — a thin adapter over the actual (and
/// independently testable) <see cref="BulkExportProcessor"/>, the same shape as
/// OutboxDispatcherRecurringJob. Lives in Infrastructure, not Worker, specifically so the API's
/// enqueuer (HangfireBackgroundJobScheduler, also Infrastructure) and the Worker's processor
/// share the exact same compiled type — Worker can't be referenced from Api/Infrastructure, so a
/// Worker-hosted class couldn't be enqueued by type from the request thread.
/// </summary>
public sealed class BulkExportJob(
    IAppDbContext db, IFileStorage fileStorage, IPdfRenderer pdfRenderer, IExcelRenderer excelRenderer, IClock clock, IOptions<AppOptions> appOptions,
    IOrganizationBrandingProvider brandingProvider)
{
    public async Task RunAsync(Guid bulkExportId, CancellationToken cancellationToken)
    {
        var branding = await brandingProvider.GetAsync(cancellationToken, includeLogo: true);
        await BulkExportProcessor.RunAsync(
            db, fileStorage, pdfRenderer, excelRenderer, clock, appOptions.Value.PublicUrl, bulkExportId, cancellationToken, branding);
    }
}
