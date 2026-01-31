using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record FlagSubmissionCommand(FlagSubmissionRequest Request) : IRequest<Result<Guid>>;

public class FlagSubmissionCommandValidator : AbstractValidator<FlagSubmissionCommand>
{
    public FlagSubmissionCommandValidator()
    {
        RuleFor(x => x.Request.SubmissionId)
            .NotEmpty().WithMessage("Submission ID is required");

        RuleFor(x => x.Request.Reason)
            .NotEmpty().WithMessage("Flag reason is required")
            .MaximumLength(500).WithMessage("Flag reason cannot exceed 500 characters");
    }
}

public class FlagSubmissionCommandHandler : IRequestHandler<FlagSubmissionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;

    public FlagSubmissionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        INotificationService notificationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
    }

    public async Task<Result<Guid>> Handle(FlagSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await _context.ReportSubmissions
            .Include(s => s.ReportTemplate)
            .FirstOrDefaultAsync(s => s.Id == request.Request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return Result<Guid>.Failure("Submission not found");
        }

        // Check if there's already an active flag for this submission
        var existingFlag = await _context.SubmissionFlags
            .FirstOrDefaultAsync(f => f.ReportSubmissionId == request.Request.SubmissionId && f.IsActive, cancellationToken);

        if (existingFlag != null)
        {
            return Result<Guid>.Failure("This submission already has an active flag");
        }

        try
        {
            var flaggerId = _currentUserService.UserId;
            var flaggerName = _currentUserService.UserName ?? "Unknown User";

            var flag = new SubmissionFlag(
                request.Request.SubmissionId,
                flaggerId,
                flaggerName,
                request.Request.Reason);

            _context.SubmissionFlags.Add(flag);
            await _context.SaveChangesAsync(cancellationToken);

            // Send notification to National administrators
            try
            {
                // TODO: Get National administrators from user service
                // For now, we'll create a notification that can be queried
                await _notificationService.SendNotificationAsync(
                    NotificationType.SubordinateSubmission,
                    Guid.Empty, // Placeholder - should be National admin
                    "National Administrator",
                    "Submission Flagged for Attention",
                    $"Submission for '{submission.ReportTemplate?.Name ?? "Report"}' has been flagged by {flaggerName}. Reason: {request.Request.Reason}",
                    NotificationPriority.High,
                    submission.Id,
                    "ReportSubmission",
                    cancellationToken);
            }
            catch (Exception)
            {
                // Log but don't fail the operation
                // Notifications are non-critical
            }

            return Result<Guid>.Success(flag.Id, "Submission flagged successfully");
        }
        catch (ArgumentException ex)
        {
            return Result<Guid>.Failure(ex.Message);
        }
    }
}
