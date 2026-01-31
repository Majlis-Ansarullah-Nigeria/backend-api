using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record UpdateSubmissionWindowCommand(Guid Id, UpdateSubmissionWindowRequest Request) : IRequest<Result>;

public class UpdateSubmissionWindowCommandValidator : AbstractValidator<UpdateSubmissionWindowCommand>
{
    public UpdateSubmissionWindowCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Window ID is required");

        RuleFor(x => x.Request.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.Request.StartDate)
            .NotEmpty().WithMessage("Start date is required");

        RuleFor(x => x.Request.EndDate)
            .NotEmpty().WithMessage("End date is required")
            .GreaterThan(x => x.Request.StartDate).WithMessage("End date must be after start date");
    }
}

public class UpdateSubmissionWindowCommandHandler : IRequestHandler<UpdateSubmissionWindowCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationService _notificationService;

    public UpdateSubmissionWindowCommandHandler(
        IApplicationDbContext context,
        INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(UpdateSubmissionWindowCommand request, CancellationToken cancellationToken)
    {
        var window = await _context.SubmissionWindows
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);

        if (window == null)
        {
            return Result.Failure("Submission window not found");
        }

        var now = DateTime.UtcNow;
        var originalEndDate = window.EndDate;
        var isExtended = request.Request.EndDate > originalEndDate;
        var isShortened = request.Request.EndDate < originalEndDate;

        // US-5: VALIDATION - Cannot shorten deadline if new date is before current date
        if (isShortened && request.Request.EndDate < now)
        {
            return Result.Failure("Cannot shorten the deadline to a date that has already passed. The new end date must be in the future.");
        }

        window.UpdateWindow(
            request.Request.Name,
            request.Request.StartDate,
            request.Request.EndDate,
            request.Request.Description
        );

        await _context.SaveChangesAsync(cancellationToken);

        // Send notifications if deadline was extended
        if (isExtended)
        {
            try
            {
                // Get users who haven't submitted yet for this window
                var submittedUserIds = await _context.ReportSubmissions
                    .Where(s => s.SubmissionWindowId == window.Id)
                    .Select(s => s.SubmitterId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                // Get all active users (in production, filter by organization level)
                var allUsers = await _context.Users
                    .Where(u => u.IsActive && !submittedUserIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.UserName })
                    .Take(1000)
                    .ToListAsync(cancellationToken);

                var pendingUsers = allUsers.Select(u => (u.Id, u.UserName ?? "User")).ToList();

                if (pendingUsers.Any())
                {
                    // Send window extended notification
                    var title = $"Deadline Extended: {window.Name}";
                    var message = $"The submission deadline for '{window.Name}' has been extended to {request.Request.EndDate:MMMM dd, yyyy}.";

                    await _notificationService.SendBulkNotificationsAsync(
                        Domain.Entities.Reports.NotificationType.WindowExtended,
                        pendingUsers,
                        title,
                        message,
                        Domain.Entities.Reports.NotificationPriority.Normal,
                        window.Id,
                        "SubmissionWindow",
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                // Notifications are non-critical
            }
        }

        return Result.Success("Submission window updated successfully");
    }
}
