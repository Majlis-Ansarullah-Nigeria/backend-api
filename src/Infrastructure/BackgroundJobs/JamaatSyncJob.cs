using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Jamaats.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ManagementApi.Infrastructure.BackgroundJobs;

public class JamaatSyncJob : IJamaatSyncJob
{
    private readonly ISender _mediator;
    private readonly ILogger<JamaatSyncJob> _logger;

    public JamaatSyncJob(ISender mediator, ILogger<JamaatSyncJob> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting scheduled jamaat sync job");

            var result = await _mediator.Send(new FetchJamaatsFromApiCommand(), cancellationToken);

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "Jamaat sync job completed successfully. Total: {Total}, New: {New}, Updated: {Updated}, Failed: {Failed}",
                    result.Data!.TotalFetched,
                    result.Data.NewJamaats,
                    result.Data.UpdatedJamaats,
                    result.Data.FailedJamaats);
            }
            else
            {
                _logger.LogError("Jamaat sync job failed: {Errors}", string.Join(", ", result.Messages));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in jamaat sync job");
        }
    }
}
