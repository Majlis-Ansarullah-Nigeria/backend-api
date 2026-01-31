using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record ApproveSubmissionCommand(ApproveSubmissionRequest Request) : IRequest<Result>;

public class ApproveSubmissionCommandValidator : AbstractValidator<ApproveSubmissionCommand>
{
    public ApproveSubmissionCommandValidator()
    {
        RuleFor(x => x.Request.SubmissionId)
            .NotEmpty().WithMessage("Submission ID is required");
    }
}

public class ApproveSubmissionCommandHandler : IRequestHandler<ApproveSubmissionCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;

    public ApproveSubmissionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        INotificationService notificationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(ApproveSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await _context.ReportSubmissions
            .Include(s => s.ReportTemplate)
            .FirstOrDefaultAsync(s => s.Id == request.Request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return Result.Failure("Submission not found");
        }

        try
        {
            var approverId = _currentUserService.UserId;
            var approverName = _currentUserService.UserName ?? "Unknown Approver";

            submission.Approve(approverId, approverName, request.Request.Comments);

            await _context.SaveChangesAsync(cancellationToken);

            // Send notification to submitter
            try
            {
                await _notificationService.NotifySubmissionApprovedAsync(
                    submission.Id,
                    submission.SubmitterId,
                    submission.SubmitterName,
                    submission.ReportTemplate?.Name ?? "Report",
                    request.Request.Comments,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Log but don't fail the operation
                // Notifications are non-critical
            }

            return Result.Success("Submission approved successfully");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
