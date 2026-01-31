using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record UnflagSubmissionCommand(UnflagSubmissionRequest Request) : IRequest<Result>;

public class UnflagSubmissionCommandValidator : AbstractValidator<UnflagSubmissionCommand>
{
    public UnflagSubmissionCommandValidator()
    {
        RuleFor(x => x.Request.FlagId)
            .NotEmpty().WithMessage("Flag ID is required");

        RuleFor(x => x.Request.ResolutionNotes)
            .MaximumLength(500).WithMessage("Resolution notes cannot exceed 500 characters");
    }
}

public class UnflagSubmissionCommandHandler : IRequestHandler<UnflagSubmissionCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;

    public UnflagSubmissionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        INotificationService notificationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(UnflagSubmissionCommand request, CancellationToken cancellationToken)
    {
        var flag = await _context.SubmissionFlags
            .Include(f => f.ReportSubmission)
            .ThenInclude(s => s.ReportTemplate)
            .FirstOrDefaultAsync(f => f.Id == request.Request.FlagId, cancellationToken);

        if (flag == null)
        {
            return Result.Failure("Flag not found");
        }

        if (!flag.IsActive)
        {
            return Result.Failure("Flag is already resolved");
        }

        try
        {
            var resolvedById = _currentUserService.UserId;
            var resolvedByName = _currentUserService.UserName ?? "Unknown User";

            flag.Resolve(resolvedById, resolvedByName, request.Request.ResolutionNotes);

            await _context.SaveChangesAsync(cancellationToken);

            // Send notification to flagger
            try
            {
                await _notificationService.SendNotificationAsync(
                    NotificationType.CommentAdded,
                    flag.FlaggerId,
                    flag.FlaggerName,
                    "Flagged Submission Resolved",
                    $"Your flag on submission for '{flag.ReportSubmission?.ReportTemplate?.Name ?? "Report"}' has been resolved by {resolvedByName}. " +
                    (string.IsNullOrWhiteSpace(request.Request.ResolutionNotes) ? "" : $"Notes: {request.Request.ResolutionNotes}"),
                    NotificationPriority.Normal,
                    flag.ReportSubmissionId,
                    "ReportSubmission",
                    cancellationToken);
            }
            catch (Exception)
            {
                // Log but don't fail the operation
                // Notifications are non-critical
            }

            return Result.Success("Flag resolved successfully");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
