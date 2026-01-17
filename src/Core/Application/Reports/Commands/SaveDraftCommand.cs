using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record SaveDraftCommand(SaveDraftRequest Request) : IRequest<Result<Guid>>;

public class SaveDraftCommandValidator : AbstractValidator<SaveDraftCommand>
{
    public SaveDraftCommandValidator()
    {
        RuleFor(x => x.Request.ReportTemplateId)
            .NotEmpty().WithMessage("Report template ID is required");

        RuleFor(x => x.Request.ResponseData)
            .NotEmpty().WithMessage("Response data is required");
    }
}

public class SaveDraftCommandHandler : IRequestHandler<SaveDraftCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SaveDraftCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Guid>> Handle(SaveDraftCommand request, CancellationToken cancellationToken)
    {
        // Validate template exists
        var template = await _context.ReportTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Request.ReportTemplateId, cancellationToken);

        if (template == null)
        {
            return Result<Guid>.Failure("Report template not found");
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

        // Check if user already has a draft for this template
        var existingDraft = await _context.ReportSubmissions
            .FirstOrDefaultAsync(s =>
                s.ReportTemplateId == request.Request.ReportTemplateId &&
                s.SubmitterId == userId &&
                s.Status == Domain.Enums.SubmissionStatus.Draft,
                cancellationToken);

        if (existingDraft != null)
        {
            // Update existing draft
            existingDraft.UpdateResponseData(request.Request.ResponseData);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(existingDraft.Id, "Draft updated successfully");
        }

        // Create new draft
        var draft = new ReportSubmission(
            request.Request.ReportTemplateId,
            userId,
            chandaNo,
            userName,
            orgLevel,
            request.Request.ResponseData,
            email,
            muqamId,
            dilaId,
            zoneId);

        _context.ReportSubmissions.Add(draft);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(draft.Id, "Draft saved successfully");
    }
}
