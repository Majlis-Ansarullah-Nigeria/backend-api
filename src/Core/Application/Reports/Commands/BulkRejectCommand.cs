using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record BulkRejectRequest
{
    public List<Guid> SubmissionIds { get; init; } = new();
    public string Reason { get; init; } = default!;
}

public record BulkRejectCommand(BulkRejectRequest Request) : IRequest<Result<BulkApprovalResultDto>>;

public class BulkRejectCommandValidator : AbstractValidator<BulkRejectCommand>
{
    public BulkRejectCommandValidator()
    {
        RuleFor(x => x.Request.SubmissionIds)
            .NotEmpty().WithMessage("At least one submission must be selected")
            .Must(ids => ids.Count <= 100).WithMessage("Cannot reject more than 100 submissions at once");

        RuleFor(x => x.Request.Reason)
            .NotEmpty().WithMessage("Rejection reason is required")
            .MaximumLength(1000).WithMessage("Reason cannot exceed 1000 characters");
    }
}

public class BulkRejectCommandHandler : IRequestHandler<BulkRejectCommand, Result<BulkApprovalResultDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notificationService;

    public BulkRejectCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        INotificationService notificationService)
    {
        _context = context;
        _currentUser = currentUser;
        _notificationService = notificationService;
    }

    public async Task<Result<BulkApprovalResultDto>> Handle(BulkRejectCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var userName = _currentUser.UserName ?? "Unknown";

        // Get all submissions
        var submissions = await _context.ReportSubmissions
            .Where(s => request.Request.SubmissionIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        if (submissions.Count == 0)
        {
            return Result<BulkApprovalResultDto>.Failure("No submissions found");
        }

        var successCount = 0;
        var failedCount = 0;
        var errors = new List<string>();
        var rejectedSubmitters = new List<(Guid SubmitterId, string SubmitterName)>();

        foreach (var submission in submissions)
        {
            try
            {
                if (submission.Status != Domain.Enums.SubmissionStatus.Submitted)
                {
                    errors.Add($"Submission {submission.Id}: Can only reject submitted reports");
                    failedCount++;
                    continue;
                }

                submission.Reject(userId, userName, request.Request.Reason);
                rejectedSubmitters.Add((submission.SubmitterId, submission.SubmitterName));
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"Submission {submission.Id}: {ex.Message}");
                failedCount++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Send notifications to rejected submitters
        if (rejectedSubmitters.Any())
        {
            try
            {
                await _notificationService.NotifyBulkRejectedAsync(
                    successCount,
                    rejectedSubmitters,
                    request.Request.Reason,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                // Notifications are non-critical
            }
        }

        var result = new BulkApprovalResultDto
        {
            TotalRequested = request.Request.SubmissionIds.Count,
            SuccessCount = successCount,
            FailedCount = failedCount,
            Errors = errors
        };

        var message = $"Bulk rejection completed: {successCount} rejected, {failedCount} failed";

        return Result<BulkApprovalResultDto>.Success(result, message);
    }
}
