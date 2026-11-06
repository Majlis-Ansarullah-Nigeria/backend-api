using ManagementApi.Application.Reports.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ManagementApi.Infrastructure.BackgroundJobs;

/// <summary>
/// Hourly: reminds the heads of units that have not submitted, 3 days and 1 day before their own
/// due date and on the day. Each reminder is sent once per unit (see SendDueRemindersCommand).
/// </summary>
public class DeadlineReminderJob : IDeadlineReminderJob
{
    private readonly ISender _mediator;
    private readonly ILogger<DeadlineReminderJob> _logger;

    public DeadlineReminderJob(ISender mediator, ILogger<DeadlineReminderJob> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task SendDeadlineRemindersAsync()
    {
        var result = await _mediator.Send(new SendDueRemindersCommand());
        _logger.LogInformation("Deadline reminder job: {Message}", result.Messages.FirstOrDefault());
    }
}

/// <summary>
/// Interface for deadline reminder job
/// </summary>
public interface IDeadlineReminderJob
{
    Task SendDeadlineRemindersAsync();
}

/// <summary>
/// Every 15 minutes: marks reports Missed once a unit's window (after any extension) has closed.
/// </summary>
public class MissedReportsJob : IMissedReportsJob
{
    private readonly ISender _mediator;
    private readonly ILogger<MissedReportsJob> _logger;

    public MissedReportsJob(ISender mediator, ILogger<MissedReportsJob> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task MarkMissedAsync()
    {
        var result = await _mediator.Send(new MarkMissedReportsCommand());
        _logger.LogInformation("Missed reports job: {Message}", result.Messages.FirstOrDefault());
    }
}

public interface IMissedReportsJob
{
    Task MarkMissedAsync();
}
