using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetReportTemplateByIdQuery(Guid TemplateId) : IRequest<Result<ReportTemplateDto>>;

public class GetReportTemplateByIdQueryHandler : IRequestHandler<GetReportTemplateByIdQuery, Result<ReportTemplateDto>>
{
    private readonly IApplicationDbContext _context;

    public GetReportTemplateByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ReportTemplateDto>> Handle(GetReportTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await _context.ReportTemplates
            .Include(rt => rt.Sections)
                .ThenInclude(rs => rs.Questions)
            .FirstOrDefaultAsync(rt => rt.Id == request.TemplateId, cancellationToken);

        if (template == null)
        {
            return Result<ReportTemplateDto>.Failure("Report template not found");
        }

        var templateDto = new ReportTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            ReportType = template.ReportType,
            OrganizationLevel = template.OrganizationLevel,
            IsForAllMembers = template.IsForAllMembers,
            IsActive = template.IsActive,
            DisplayOrder = template.DisplayOrder,
            Sections = template.Sections
                .OrderBy(s => s.DisplayOrder)
                .Select(s => new ReportSectionDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Description = s.Description,
                    DisplayOrder = s.DisplayOrder,
                    IsRequired = s.IsRequired,
                    Questions = s.Questions
                        .OrderBy(q => q.DisplayOrder)
                        .Select(q => new ReportQuestionDto
                        {
                            Id = q.Id,
                            QuestionText = q.QuestionText,
                            HelpText = q.HelpText,
                            QuestionType = q.QuestionType.ToString(),
                            Options = q.Options,
                            IsRequired = q.IsRequired,
                            DisplayOrder = q.DisplayOrder,
                            ValidationRules = q.ValidationRules
                        }).ToList()
                }).ToList(),
            CreatedOn = template.CreatedOn
        };

        return Result<ReportTemplateDto>.Success(templateDto);
    }
}