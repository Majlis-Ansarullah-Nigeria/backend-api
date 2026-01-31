using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record AddCommentCommand(AddCommentRequest Request) : IRequest<Result<Guid>>;

public class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.Request.SubmissionId)
            .NotEmpty().WithMessage("Submission ID is required");

        RuleFor(x => x.Request.Content)
            .NotEmpty().WithMessage("Comment content is required")
            .MaximumLength(2000).WithMessage("Comment content cannot exceed 2000 characters");
    }
}

public class AddCommentCommandHandler : IRequestHandler<AddCommentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;

    public AddCommentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        INotificationService notificationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
    }

    public async Task<Result<Guid>> Handle(AddCommentCommand request, CancellationToken cancellationToken)
    {
        // Verify submission exists
        var submission = await _context.ReportSubmissions
            .Include(s => s.ReportTemplate)
            .FirstOrDefaultAsync(s => s.Id == request.Request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return Result<Guid>.Failure("Submission not found");
        }

        // If replying to a comment, verify parent comment exists
        if (request.Request.ParentCommentId.HasValue)
        {
            var parentComment = await _context.SubmissionComments
                .FirstOrDefaultAsync(c => c.Id == request.Request.ParentCommentId.Value, cancellationToken);

            if (parentComment == null)
            {
                return Result<Guid>.Failure("Parent comment not found");
            }

            if (parentComment.ReportSubmissionId != request.Request.SubmissionId)
            {
                return Result<Guid>.Failure("Parent comment does not belong to this submission");
            }

            if (parentComment.IsDeleted)
            {
                return Result<Guid>.Failure("Cannot reply to a deleted comment");
            }
        }

        try
        {
            var commenterId = _currentUserService.UserId;
            var commenterName = _currentUserService.UserName ?? "Unknown User";

            var comment = new SubmissionComment(
                request.Request.SubmissionId,
                commenterId,
                commenterName,
                request.Request.Content,
                request.Request.ParentCommentId);

            _context.SubmissionComments.Add(comment);
            await _context.SaveChangesAsync(cancellationToken);

            // Send notification to submission owner and other commenters
            try
            {
                // Notify submission owner if commenter is not the owner
                if (submission.SubmitterId != commenterId)
                {
                    await _notificationService.SendNotificationAsync(
                        NotificationType.CommentAdded,
                        submission.SubmitterId,
                        submission.SubmitterName ?? "User",
                        "New Comment on Your Submission",
                        $"{commenterName} commented on your submission for '{submission.ReportTemplate?.Name ?? "Report"}': {request.Request.Content.Substring(0, Math.Min(100, request.Request.Content.Length))}...",
                        NotificationPriority.Normal,
                        submission.Id,
                        "ReportSubmission",
                        cancellationToken);
                }

                // If this is a reply, notify the parent comment author
                if (request.Request.ParentCommentId.HasValue)
                {
                    var parentComment = await _context.SubmissionComments
                        .FirstOrDefaultAsync(c => c.Id == request.Request.ParentCommentId.Value, cancellationToken);

                    if (parentComment != null && parentComment.CommenterId != commenterId)
                    {
                        await _notificationService.SendNotificationAsync(
                            NotificationType.CommentAdded,
                            parentComment.CommenterId,
                            parentComment.CommenterName,
                            "Reply to Your Comment",
                            $"{commenterName} replied to your comment: {request.Request.Content.Substring(0, Math.Min(100, request.Request.Content.Length))}...",
                            NotificationPriority.Normal,
                            submission.Id,
                            "ReportSubmission",
                            cancellationToken);
                    }
                }
            }
            catch (Exception)
            {
                // Log but don't fail the operation
                // Notifications are non-critical
            }

            return Result<Guid>.Success(comment.Id, "Comment added successfully");
        }
        catch (ArgumentException ex)
        {
            return Result<Guid>.Failure(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<Guid>.Failure(ex.Message);
        }
    }
}
