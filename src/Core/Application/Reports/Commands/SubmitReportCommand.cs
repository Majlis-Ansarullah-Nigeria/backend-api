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
        // Get current user info first to declare variables
        var userId = _currentUserService.UserId;
        var chandaNo = _currentUserService.ChandaNo ?? "UNKNOWN";
        var userName = _currentUserService.UserName ?? "Unknown User";
        var email = _currentUserService.Email;
        var orgLevel = _currentUserService.OrganizationLevel;
        var currentUserMuqamId = _currentUserService.MuqamId;
        var currentUserDilaId = _currentUserService.DilaId;
        var currentUserZoneId = _currentUserService.ZoneId;

        // Initialize organization IDs
        Guid? muqamId = null;
        Guid? dilaId = null;
        Guid? zoneId = null;

        // Get organization information from the Member table using ChandaNo
        var member = await _context.Members
            .Include(m => m.Muqam)
                .ThenInclude(m => m.Dila)
                    .ThenInclude(d => d.Zone)
            .FirstOrDefaultAsync(m => m.ChandaNo == chandaNo, cancellationToken);

        if (member != null)
        {
            // First, try to get organization info via Jamaat mapping (if JamaatId exists)
            if (member.JamaatId.HasValue)
            {
                var jamaat = await _context.Jamaats
                    .Include(j => j.Muqam)
                        .ThenInclude(m => m.Dila)
                            .ThenInclude(d => d.Zone)
                    .FirstOrDefaultAsync(j => j.JamaatId == member.JamaatId.Value, cancellationToken);

                if (jamaat?.MuqamId != null)
                {
                    muqamId = jamaat.MuqamId;

                    var muqam = jamaat.Muqam;
                    if (muqam != null)
                    {
                        dilaId = muqam.DilaId;

                        if (muqam.Dila != null)
                        {
                            zoneId = muqam.Dila.ZoneId;
                        }
                    }
                }
            }

            // If no organization info from Jamaat, use direct associations
            if (!muqamId.HasValue && member.MuqamId.HasValue)
            {
                var muqam = member.Muqam;
                if (muqam != null)
                {
                    muqamId = muqam.Id;
                    dilaId = muqam.DilaId;
                    zoneId = muqam.Dila?.ZoneId;
                }
            }
        }
        else
        {
            // Fallback to CurrentUserService if member not found
            muqamId = currentUserMuqamId;
            dilaId = currentUserDilaId;
            zoneId = currentUserZoneId;
        }

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

            // Check if an organization unit has already submitted to this window
            // The check depends on the user's organization level
            IQueryable<ReportSubmission> query = _context.ReportSubmissions.Where(s => s.SubmissionWindowId == request.Request.SubmissionWindowId.Value);

            if (orgLevel == Domain.Enums.OrganizationLevel.Muqam && muqamId.HasValue)
            {
                query = query.Where(s => s.MuqamId == muqamId);
            }
            else if (orgLevel == Domain.Enums.OrganizationLevel.Dila && dilaId.HasValue)
            {
                query = query.Where(s => s.DilaId == dilaId);
            }
            else if (orgLevel == Domain.Enums.OrganizationLevel.Zone && zoneId.HasValue)
            {
                query = query.Where(s => s.ZoneId == zoneId);
            }
            else if (orgLevel == Domain.Enums.OrganizationLevel.National)
            {
                query = query.Where(s => s.OrganizationLevel == Domain.Enums.OrganizationLevel.National);
            }
            else
            {
                // If organization level doesn't match known levels, skip the duplicate check
            }

            var existingSubmission = await query.FirstOrDefaultAsync(cancellationToken);

            if (existingSubmission != null)
            {
                var orgLevelName = orgLevel.ToString();
                return Result<Guid>.Failure($"A report has already been submitted for this {orgLevelName} in this submission window");
            }
        }

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
