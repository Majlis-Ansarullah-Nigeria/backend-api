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

    public CreateSubmissionWindowCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateSubmissionWindowCommand request, CancellationToken cancellationToken)
    {
        // Verify template exists
        var templateExists = await _context.ReportTemplates
            .AnyAsync(t => t.Id == request.Request.ReportTemplateId, cancellationToken);

        if (!templateExists)
        {
            return Result<Guid>.Failure("Report template not found");
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

        return Result<Guid>.Success(window.Id, "Submission window created successfully");
    }
}
