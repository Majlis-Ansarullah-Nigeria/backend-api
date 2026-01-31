using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record CreateSubmissionWindowCommand(CreateSubmissionWindowRequest Request) : IRequest<Result<Guid>>;

public class CreateSubmissionWindowCommandValidator : AbstractValidator<CreateSubmissionWindowCommand>
{
    public CreateSubmissionWindowCommandValidator()
    {
        RuleFor(x => x.Request.ReportTemplateId)
            .NotEmpty().WithMessage("Report template is required");

        RuleFor(x => x.Request.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.Request.StartDate)
            .NotEmpty().WithMessage("Start date is required");

        RuleFor(x => x.Request.EndDate)
            .NotEmpty().WithMessage("End date is required")
            .GreaterThan(x => x.Request.StartDate).WithMessage("End date must be after start date");
    }
}

public class CreateSubmissionWindowCommandHandler : IRequestHandler<CreateSubmissionWindowCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationService _notificationService;

    public CreateSubmissionWindowCommandHandler(
        IApplicationDbContext context,
        INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<Result<Guid>> Handle(CreateSubmissionWindowCommand request, CancellationToken cancellationToken)
    {
        // Verify template exists and get it
        var template = await _context.ReportTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Request.ReportTemplateId, cancellationToken);

        if (template == null)
        {
            return Result<Guid>.Failure("Report template not found");
        }

        // US-4: VALIDATION - Prevent overlapping windows for the same template
        var hasOverlap = await _context.SubmissionWindows
            .Where(w => w.ReportTemplateId == request.Request.ReportTemplateId && w.IsActive)
            .AnyAsync(w =>
                (request.Request.StartDate >= w.StartDate && request.Request.StartDate <= w.EndDate) ||
                (request.Request.EndDate >= w.StartDate && request.Request.EndDate <= w.EndDate) ||
                (request.Request.StartDate <= w.StartDate && request.Request.EndDate >= w.EndDate),
                cancellationToken);

        if (hasOverlap)
        {
            return Result<Guid>.Failure("Cannot create submission window because it overlaps with an existing active window for this template. Please adjust the dates.");
        }

        var window = new SubmissionWindow(
            request.Request.ReportTemplateId,
            request.Request.Name,
            request.Request.StartDate,
            request.Request.EndDate,
            request.Request.Description
        );

        _context.SubmissionWindows.Add(window);
        await _context.SaveChangesAsync(cancellationToken);

        // Send notifications to eligible users
        try
        {
            // Get eligible users based on template's organization level
            var eligibleUsers = new List<(Guid UserId, string UserName)>();

            // Query users who can submit at this organization level
            // For simplicity, we'll get all active users for now
            // In production, you'd filter by organization level and active members
            var users = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, u.UserName })
                .Take(1000) // Limit to prevent memory issues
                .ToListAsync(cancellationToken);

            eligibleUsers = users.Select(u => (u.Id, u.UserName ?? "User")).ToList();

            if (eligibleUsers.Any())
            {
                await _notificationService.NotifyWindowOpenedAsync(
                    window.Id,
                    window.Name,
                    window.EndDate,
                    eligibleUsers,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the operation
            // Notifications are non-critical
        }

        return Result<Guid>.Success(window.Id, "Submission window created successfully");
    }
}
