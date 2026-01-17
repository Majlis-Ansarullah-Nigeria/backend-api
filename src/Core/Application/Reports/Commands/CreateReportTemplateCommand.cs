using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record CreateReportTemplateCommand(CreateReportTemplateRequest Request) : IRequest<Result<Guid>>;

public class CreateReportTemplateCommandValidator : AbstractValidator<CreateReportTemplateCommand>
{
    public CreateReportTemplateCommandValidator()
    {
        RuleFor(x => x.Request.Name)
            .NotEmpty().WithMessage("Template name is required")
            .MaximumLength(200).WithMessage("Template name must not exceed 200 characters");

        RuleFor(x => x.Request.ReportType)
            .NotEmpty().WithMessage("Report type is required");

        RuleFor(x => x.Request.Sections)
            .NotEmpty().WithMessage("At least one section is required");
    }
}

public class CreateReportTemplateCommandHandler : IRequestHandler<CreateReportTemplateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateReportTemplateCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateReportTemplateCommand request, CancellationToken cancellationToken)
    {
        // Check if template with same name already exists
        var exists = await _context.ReportTemplates
            .AnyAsync(t => t.Name == request.Request.Name, cancellationToken);

        if (exists)
        {
            return Result<Guid>.Failure("A report template with this name already exists");
        }

        // Create template
        var template = new ReportTemplate(
            request.Request.Name,
            request.Request.ReportType,
            request.Request.OrganizationLevel,
            request.Request.IsForAllMembers,
            request.Request.Description,
            request.Request.DisplayOrder);

        _context.ReportTemplates.Add(template);

        // Create sections
        foreach (var sectionRequest in request.Request.Sections)
        {
            var section = new ReportSection(
                template.Id,
                sectionRequest.Title,
                sectionRequest.Description,
                sectionRequest.DisplayOrder,
                sectionRequest.IsRequired);

            template.AddSection(section);
            _context.ReportSections.Add(section);

            // Create questions
            foreach (var questionRequest in sectionRequest.Questions)
            {
                if (!Enum.TryParse<QuestionType>(questionRequest.QuestionType, true, out var questionType))
                {
                    return Result<Guid>.Failure($"Invalid question type: {questionRequest.QuestionType}");
                }

                var question = new ReportQuestion(
                    section.Id,
                    questionRequest.QuestionText,
                    questionType,
                    questionRequest.HelpText,
                    questionRequest.Options,
                    questionRequest.IsRequired,
                    questionRequest.DisplayOrder,
                    questionRequest.ValidationRules);

                section.AddQuestion(question);
                _context.ReportQuestions.Add(question);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(template.Id, "Report template created successfully");
    }
}
