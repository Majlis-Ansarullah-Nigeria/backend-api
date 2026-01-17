using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record SubmitReportCommand(SubmitReportRequest Request) : IRequest<Result<Guid>>;

public class SubmitReportCommandValidator : AbstractValidator<SubmitReportCommand>
{
    public SubmitReportCommandValidator()
    {
        RuleFor(x => x.Request.ReportTemplateId)
            .NotEmpty().WithMessage("Report template ID is required");

        RuleFor(x => x.Request.ResponseData)
            .NotEmpty().WithMessage("Response data is required");
    }
}

public class SubmitReportCommandHandler : IRequestHandler<SubmitReportCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SubmitReportCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Guid>> Handle(SubmitReportCommand request, CancellationToken cancellationToken)
    {
        // Validate template exists
        var template = await _context.ReportTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Request.ReportTemplateId, cancellationToken);

        if (template == null)
        {
            return Result<Guid>.Failure("Report template not found");
        }

        if (!template.IsActive)
        {
            return Result<Guid>.Failure("Report template is not active");
        }

        // Validate submission window if provided
        if (request.Request.SubmissionWindowId.HasValue)
        {
            var window = await _context.SubmissionWindows
                .FirstOrDefaultAsync(w => w.Id == request.Request.SubmissionWindowId.Value, cancellationToken);

            if (window == null)
            {
                return Result<Guid>.Failure("Submission window not found");
            }

            if (!window.IsOpen())
            {
                return Result<Guid>.Failure("Submission window is not open");
            }
        }

        // Get current user info
        var userId = _currentUserService.UserId;
        var chandaNo = _currentUserService.ChandaNo ?? "UNKNOWN";
        var userName = _currentUserService.UserName ?? "Unknown User";
        var email = _currentUserService.Email;
        var muqamId = _currentUserService.MuqamId;
        var dilaId = _currentUserService.DilaId;
        var zoneId = _currentUserService.ZoneId;
        var orgLevel = _currentUserService.OrganizationLevel;

        // Create submission
        var submission = new ReportSubmission(
            request.Request.ReportTemplateId,
            userId,
            chandaNo,
            userName,
            orgLevel,
            request.Request.ResponseData,
            email,
            muqamId,
            dilaId,
            zoneId,
            request.Request.SubmissionWindowId);

        // Submit immediately
        submission.Submit();

        _context.ReportSubmissions.Add(submission);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(submission.Id, "Report submitted successfully");
    }
}
