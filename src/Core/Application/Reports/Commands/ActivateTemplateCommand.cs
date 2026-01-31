using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record ActivateTemplateCommand(Guid TemplateId) : IRequest<Result>;

public class ActivateTemplateCommandValidator : AbstractValidator<ActivateTemplateCommand>
{
    public ActivateTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("Template ID is required");
    }
}

public class ActivateTemplateCommandHandler : IRequestHandler<ActivateTemplateCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public ActivateTemplateCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(ActivateTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _context.ReportTemplates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template == null)
        {
            return Result.Failure("Template not found");
        }

        if (template.IsActive)
        {
            return Result.Failure("Template is already active");
        }

        try
        {
            template.Activate();
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success("Template activated successfully");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
