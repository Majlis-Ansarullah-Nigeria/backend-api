using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Members.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ManagementApi.Infrastructure.BackgroundJobs;

public class MemberSyncJob : IMemberSyncJob
{
    private readonly ISender _mediator;
    private readonly ILogger<MemberSyncJob> _logger;

    public MemberSyncJob(ISender mediator, ILogger<MemberSyncJob> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting scheduled member sync job");

            var result = await _mediator.Send(new FetchMembersFromGatewayCommand(), cancellationToken);

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "Member sync job completed successfully. Total: {Total}, New: {New}, Updated: {Updated}, Failed: {Failed}",
                    result.Data!.TotalFetched,
                    result.Data.NewMembers,
                    result.Data.UpdatedMembers,
                    result.Data.FailedMembers);
            }
            else
            {
                _logger.LogError("Member sync job failed: {Errors}", string.Join(", ", result.Messages));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in member sync job");
        }
    }
}
