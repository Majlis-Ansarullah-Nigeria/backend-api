using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

// Review actions are for the National Secretariat (NationalAdmin, SuperAdmin) only.
// Observers (parent Dila/Zone heads) and the submitting unit can never review.

public record AcknowledgeSubmissionCommand(AcknowledgeSubmissionRequest Request) : IRequest<Result>;

public record ReturnSubmissionCommand(ReturnSubmissionRequest Request) : IRequest<Result>;

public record ReopenSubmissionCommand(ReopenSubmissionRequest Request) : IRequest<Result>;

public record StartReviewCommand(Guid SubmissionId) : IRequest<Result>;

public record WithdrawSubmissionCommand(Guid SubmissionId) : IRequest<Result>;

public record BulkAcknowledgeRequest
{
    public List<Guid> SubmissionIds { get; init; } = new();
    public string? Comments { get; init; }
}

public record BulkReturnRequest
{
    public List<Guid> SubmissionIds { get; init; } = new();
    public string Reason { get; init; } = default!;
}

public record BulkAcknowledgeCommand(BulkAcknowledgeRequest Request) : IRequest<Result<BulkReviewResultDto>>;

public record BulkReturnCommand(BulkReturnRequest Request) : IRequest<Result<BulkReviewResultDto>>;

public record BulkReviewResultDto
{
    public int TotalRequested { get; init; }
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public List<string> Errors { get; init; } = new();
}

public class ReturnSubmissionCommandValidator : AbstractValidator<ReturnSubmissionCommand>
{
    public ReturnSubmissionCommandValidator()
    {
        RuleFor(x => x.Request.Reason)
            .NotEmpty().WithMessage("A reason is required to return a report")
            .MaximumLength(1000).WithMessage("Reason cannot exceed 1000 characters");
    }
}

public class ReopenSubmissionCommandValidator : AbstractValidator<ReopenSubmissionCommand>
{
    public ReopenSubmissionCommandValidator()
    {
        RuleFor(x => x.Request.Reason)
            .NotEmpty().WithMessage("A reason is required to reopen a report")
            .MaximumLength(1000).WithMessage("Reason cannot exceed 1000 characters");
    }
}

public class BulkAcknowledgeCommandValidator : AbstractValidator<BulkAcknowledgeCommand>
{
    public BulkAcknowledgeCommandValidator()
    {
        RuleFor(x => x.Request.SubmissionIds)
            .NotEmpty().WithMessage("At least one submission must be selected")
            .Must(ids => ids.Count <= 100).WithMessage("Cannot acknowledge more than 100 submissions at once");
    }
}

public class BulkReturnCommandValidator : AbstractValidator<BulkReturnCommand>
{
    public BulkReturnCommandValidator()
    {
        RuleFor(x => x.Request.SubmissionIds)
            .NotEmpty().WithMessage("At least one submission must be selected")
            .Must(ids => ids.Count <= 100).WithMessage("Cannot return more than 100 submissions at once");

        RuleFor(x => x.Request.Reason)
            .NotEmpty().WithMessage("A reason is required to return reports")
            .MaximumLength(1000).WithMessage("Reason cannot exceed 1000 characters");
    }
}

public class ReviewSubmissionCommandHandlers :
    IRequestHandler<AcknowledgeSubmissionCommand, Result>,
    IRequestHandler<ReturnSubmissionCommand, Result>,
    IRequestHandler<ReopenSubmissionCommand, Result>,
    IRequestHandler<StartReviewCommand, Result>,
    IRequestHandler<WithdrawSubmissionCommand, Result>,
    IRequestHandler<BulkAcknowledgeCommand, Result<BulkReviewResultDto>>,
    IRequestHandler<BulkReturnCommand, Result<BulkReviewResultDto>>
{
    public const string Forbidden = "FORBIDDEN: Only the National Secretariat can review reports";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notificationService;
    private readonly TimeProvider _clock;

    public ReviewSubmissionCommandHandlers(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        INotificationService notificationService,
        TimeProvider clock)
    {
        _context = context;
        _currentUser = currentUser;
        _notificationService = notificationService;
        _clock = clock;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private string ReviewerName => _currentUser.UserName ?? "National Secretariat";

    public async Task<Result> Handle(AcknowledgeSubmissionCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result.Failure(Forbidden);
        }

        var submission = await LoadAsync(request.Request.SubmissionId, cancellationToken);
        if (submission is null)
        {
            return Result.Failure("Submission not found");
        }

        try
        {
            submission.Acknowledge(_currentUser.UserId, ReviewerName, Now, request.Request.Comments);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await NotifyQuietly(() => _notificationService.NotifySubmissionApprovedAsync(
            submission.Id,
            submission.SubmitterId,
            submission.SubmitterName,
            submission.ReportTemplate?.Name ?? "Report",
            request.Request.Comments,
            cancellationToken));

        return Result.Success("Report acknowledged");
    }

    public async Task<Result> Handle(ReturnSubmissionCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result.Failure(Forbidden);
        }

        var submission = await LoadAsync(request.Request.SubmissionId, cancellationToken);
        if (submission is null)
        {
            return Result.Failure("Submission not found");
        }

        try
        {
            submission.Return(_currentUser.UserId, ReviewerName, request.Request.Reason, Now);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await NotifyQuietly(() => _notificationService.NotifySubmissionRejectedAsync(
            submission.Id,
            submission.SubmitterId,
            submission.SubmitterName,
            submission.ReportTemplate?.Name ?? "Report",
            request.Request.Reason,
            cancellationToken));

        return Result.Success("Report returned for correction");
    }

    public async Task<Result> Handle(ReopenSubmissionCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result.Failure(Forbidden);
        }

        var submission = await LoadAsync(request.Request.SubmissionId, cancellationToken);
        if (submission is null)
        {
            return Result.Failure("Submission not found");
        }

        try
        {
            submission.Reopen(_currentUser.UserId, ReviewerName, request.Request.Reason, Now);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success("Report reopened for review");
    }

    public async Task<Result> Handle(StartReviewCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result.Failure(Forbidden);
        }

        var submission = await LoadAsync(request.SubmissionId, cancellationToken);
        if (submission is null)
        {
            return Result.Failure("Submission not found");
        }

        submission.StartReview(Now, ReviewerName);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> Handle(WithdrawSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await LoadAsync(request.SubmissionId, cancellationToken);

        // Only the owning unit may withdraw; anyone else gets "not found" so ids are not probed.
        if (submission is null || !ReportingScope.OwnsReport(_currentUser, submission))
        {
            return Result.Failure("Submission not found");
        }

        if (submission.SubmissionWindow is { } window && !window.IsOpenFor(Now, submission.OrganizationLevel, submission.UnitId))
        {
            return Result.Failure("WINDOW_CLOSED: This submission window is closed, so the report can no longer be withdrawn");
        }

        try
        {
            submission.Withdraw();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success("Report withdrawn to draft");
    }

    public async Task<Result<BulkReviewResultDto>> Handle(BulkAcknowledgeCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result<BulkReviewResultDto>.Failure(Forbidden);
        }

        var (result, done) = await BulkAsync(
            request.Request.SubmissionIds,
            s => s.Acknowledge(_currentUser.UserId, ReviewerName, Now, request.Request.Comments),
            cancellationToken);

        if (done.Count > 0)
        {
            await NotifyQuietly(() => _notificationService.NotifyBulkApprovedAsync(
                done.Count, done, request.Request.Comments, cancellationToken));
        }

        return Result<BulkReviewResultDto>.Success(result,
            $"Bulk acknowledge completed: {result.SuccessCount} acknowledged, {result.FailedCount} failed");
    }

    public async Task<Result<BulkReviewResultDto>> Handle(BulkReturnCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result<BulkReviewResultDto>.Failure(Forbidden);
        }

        var (result, done) = await BulkAsync(
            request.Request.SubmissionIds,
            s => s.Return(_currentUser.UserId, ReviewerName, request.Request.Reason, Now),
            cancellationToken);

        if (done.Count > 0)
        {
            await NotifyQuietly(() => _notificationService.NotifyBulkRejectedAsync(
                done.Count, done, request.Request.Reason, cancellationToken));
        }

        return Result<BulkReviewResultDto>.Success(result,
            $"Bulk return completed: {result.SuccessCount} returned, {result.FailedCount} failed");
    }

    private Task<ReportSubmission?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        _context.ReportSubmissions
            .Include(s => s.ReportTemplate)
            .Include(s => s.SubmissionWindow!.Extensions)
            .Include(s => s.Approvals)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    private async Task<(BulkReviewResultDto Result, List<(Guid SubmitterId, string SubmitterName)> Done)> BulkAsync(
        List<Guid> ids,
        Action<ReportSubmission> action,
        CancellationToken cancellationToken)
    {
        var submissions = await _context.ReportSubmissions
            .Include(s => s.Approvals)
            .Where(s => ids.Contains(s.Id))
            .ToListAsync(cancellationToken);

        var errors = ids.Except(submissions.Select(s => s.Id))
            .Select(id => $"Submission {id}: not found")
            .ToList();
        var done = new List<(Guid SubmitterId, string SubmitterName)>();

        foreach (var submission in submissions)
        {
            try
            {
                action(submission);
                done.Add((submission.SubmitterId, submission.SubmitterName));
            }
            catch (InvalidOperationException ex)
            {
                errors.Add($"Submission {submission.Id}: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return (new BulkReviewResultDto
        {
            TotalRequested = ids.Count,
            SuccessCount = done.Count,
            FailedCount = errors.Count,
            Errors = errors
        }, done);
    }

    // Notifications are non-critical: a failure must not undo a review action that was saved.
    private static async Task NotifyQuietly(Func<Task> send)
    {
        try
        {
            await send();
        }
        catch
        {
            // Deliberately ignored, as in the rest of the reports module.
        }
    }
}
