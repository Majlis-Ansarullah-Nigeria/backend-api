using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record BulkApproveRequest
{
    public List<Guid> SubmissionIds { get; init; } = new();
    public string? Comments { get; init; }
}

public record BulkApproveCommand(BulkApproveRequest Request) : IRequest<Result<BulkApprovalResultDto>>;

public class BulkApproveCommandValidator : AbstractValidator<BulkApproveCommand>
{
    public BulkApproveCommandValidator()
    {
        RuleFor(x => x.Request.SubmissionIds)
            .NotEmpty().WithMessage("At least one submission must be selected")
            .Must(ids => ids.Count <= 100).WithMessage("Cannot approve more than 100 submissions at once");

        RuleFor(x => x.Request.Comments)
            .MaximumLength(1000).WithMessage("Comments cannot exceed 1000 characters");
    }
}

public class BulkApproveCommandHandler : IRequestHandler<BulkApproveCommand, Result<BulkApprovalResultDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notificationService;

    public BulkApproveCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        INotificationService notificationService)
    {
        _context = context;
        _currentUser = currentUser;
        _notificationService = notificationService;
    }

    public async Task<Result<BulkApprovalResultDto>> Handle(BulkApproveCommand request, CancellationToken cancellationToken)
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
        var approvedSubmitters = new List<(Guid SubmitterId, string SubmitterName)>();

        foreach (var submission in submissions)
        {
            try
            {
                if (submission.Status != Domain.Enums.SubmissionStatus.Submitted)
                {
                    errors.Add($"Submission {submission.Id}: Can only approve submitted reports");
                    failedCount++;
                    continue;
                }

                submission.Approve(userId, userName, request.Request.Comments);
                approvedSubmitters.Add((submission.SubmitterId, submission.SubmitterName));
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"Submission {submission.Id}: {ex.Message}");
                failedCount++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Send notifications to approved submitters
        if (approvedSubmitters.Any())
        {
            try
            {
                await _notificationService.NotifyBulkApprovedAsync(
                    successCount,
                    approvedSubmitters,
                    request.Request.Comments,
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

        var message = $"Bulk approval completed: {successCount} approved, {failedCount} failed";

        return Result<BulkApprovalResultDto>.Success(result, message);
    }
}

public record BulkApprovalResultDto
{
    public int TotalRequested { get; init; }
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public List<string> Errors { get; init; } = new();
}
