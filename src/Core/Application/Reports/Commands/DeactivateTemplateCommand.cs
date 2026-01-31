using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record DeactivateTemplateCommand(Guid TemplateId) : IRequest<Result>;

public class DeactivateTemplateCommandValidator : AbstractValidator<DeactivateTemplateCommand>
{
    public DeactivateTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("Template ID is required");
    }
}

public class DeactivateTemplateCommandHandler : IRequestHandler<DeactivateTemplateCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public DeactivateTemplateCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(DeactivateTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _context.ReportTemplates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template == null)
        {
            return Result.Failure("Template not found");
        }

        if (!template.IsActive)
        {
            return Result.Failure("Template is already inactive");
        }

        // US-3: VALIDATION - Cannot deactivate if active windows exist
        var hasActiveWindows = await _context.SubmissionWindows
            .AnyAsync(w => w.ReportTemplateId == request.TemplateId && w.IsActive, cancellationToken);

        if (hasActiveWindows)
        {
            return Result.Failure("Cannot deactivate template because it has active submission windows. Please deactivate or close the windows first.");
        }

        try
        {
            template.Deactivate();
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success("Template deactivated successfully");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
