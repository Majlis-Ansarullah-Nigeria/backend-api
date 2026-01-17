using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record RejectSubmissionCommand(RejectSubmissionRequest Request) : IRequest<Result>;

public class RejectSubmissionCommandValidator : AbstractValidator<RejectSubmissionCommand>
{
    public RejectSubmissionCommandValidator()
    {
        RuleFor(x => x.Request.SubmissionId)
            .NotEmpty().WithMessage("Submission ID is required");

        RuleFor(x => x.Request.Reason)
            .NotEmpty().WithMessage("Rejection reason is required")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters");
    }
}

public class RejectSubmissionCommandHandler : IRequestHandler<RejectSubmissionCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public RejectSubmissionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(RejectSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await _context.ReportSubmissions
            .FirstOrDefaultAsync(s => s.Id == request.Request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return Result.Failure("Submission not found");
        }

        try
        {
            var approverId = _currentUserService.UserId;
            var approverName = _currentUserService.UserName ?? "Unknown Approver";

            submission.Reject(approverId, approverName, request.Request.Reason);

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success("Submission rejected successfully");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
