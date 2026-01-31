using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Domain.Entities.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ManagementApi.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job that sends deadline reminder notifications to users
/// who haven't submitted their reports yet
/// </summary>
public class DeadlineReminderJob : IDeadlineReminderJob
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DeadlineReminderJob> _logger;

    public DeadlineReminderJob(
        IApplicationDbContext context,
        INotificationService notificationService,
        ILogger<DeadlineReminderJob> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Sends deadline reminders for submission windows closing soon
    /// Runs daily to check for windows closing in 3 days, 1 day, or on the deadline day
    /// </summary>
    public async Task SendDeadlineRemindersAsync()
    {
        try
        {
            _logger.LogInformation("Starting deadline reminder job");

            var now = DateTime.UtcNow.Date;
            var threeDaysFromNow = now.AddDays(3);
            var oneDayFromNow = now.AddDays(1);

            // Get active windows that are closing soon
            var upcomingWindows = await _context.SubmissionWindows
                .Where(w => w.IsActive && w.EndDate >= now && w.EndDate <= threeDaysFromNow)
                .ToListAsync();

            _logger.LogInformation("Found {Count} submission windows with upcoming deadlines", upcomingWindows.Count);

            foreach (var window in upcomingWindows)
            {
                try
                {
                    var daysRemaining = (int)(window.EndDate.Date - now).TotalDays;

                    // Only send reminders at specific intervals: 3 days, 1 day, or same day
                    if (daysRemaining != 3 && daysRemaining != 1 && daysRemaining != 0)
                    {
                        continue;
                    }

                    // Get users who have already submitted for this window
                    var submittedUserIds = await _context.ReportSubmissions
                        .Where(s => s.SubmissionWindowId == window.Id)
                        .Select(s => s.SubmitterId)
                        .Distinct()
                        .ToListAsync();

                    // Get all active users who haven't submitted yet
                    // In production, you'd filter by organization level based on the template
                    var pendingUsers = await _context.Users
                        .Where(u => u.IsActive && !submittedUserIds.Contains(u.Id))
                        .Select(u => new { u.Id, u.UserName })
                        .Take(1000) // Limit to prevent memory issues
                        .ToListAsync();

                    if (pendingUsers.Any())
                    {
                        var userList = pendingUsers.Select(u => (u.Id, u.UserName ?? "User")).ToList();

                        await _notificationService.NotifyDeadlineApproachingAsync(
                            window.Id,
                            window.Name,
                            window.EndDate,
                            daysRemaining,
                            userList);

                        _logger.LogInformation(
                            "Sent deadline reminder for window {WindowName} to {Count} users ({Days} days remaining)",
                            window.Name, userList.Count, daysRemaining);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending deadline reminder for window {WindowId}", window.Id);
                    // Continue with next window
                }
            }

            _logger.LogInformation("Deadline reminder job completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in deadline reminder job");
            throw;
        }
    }
}

/// <summary>
/// Interface for deadline reminder job
/// </summary>
public interface IDeadlineReminderJob
{
    Task SendDeadlineRemindersAsync();
}
