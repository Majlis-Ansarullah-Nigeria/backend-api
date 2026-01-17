using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetReportTemplatesQuery(bool? OnlyActive = null) : IRequest<Result<List<ReportTemplateDto>>>;

public class GetReportTemplatesQueryHandler : IRequestHandler<GetReportTemplatesQuery, Result<List<ReportTemplateDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetReportTemplatesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ReportTemplateDto>>> Handle(GetReportTemplatesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ReportTemplates
            .Include(t => t.Sections.OrderBy(s => s.DisplayOrder))
            .ThenInclude(s => s.Questions.OrderBy(q => q.DisplayOrder))
            .AsQueryable();

        if (request.OnlyActive.HasValue && request.OnlyActive.Value)
        {
            query = query.Where(t => t.IsActive);
        }

        var templates = await query
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var templateDtos = templates.Select(t => new ReportTemplateDto
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            ReportType = t.ReportType,
            IsActive = t.IsActive,
            DisplayOrder = t.DisplayOrder,
            CreatedOn = t.CreatedOn,
            Sections = t.Sections.Select(s => new ReportSectionDto
            {
                Id = s.Id,
                Title = s.Title,
                Description = s.Description,
                DisplayOrder = s.DisplayOrder,
                IsRequired = s.IsRequired,
                Questions = s.Questions.Select(q => new ReportQuestionDto
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
            }).ToList()
        }).ToList();

        return Result<List<ReportTemplateDto>>.Success(templateDtos);
    }
}
