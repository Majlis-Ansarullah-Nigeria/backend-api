using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using ManagementApi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetUserAccessibleTemplateQuery(Guid TemplateId, Guid? UserId = null) : IRequest<Result<ReportTemplateDto>>;

public class GetUserAccessibleTemplateQueryHandler : IRequestHandler<GetUserAccessibleTemplateQuery, Result<ReportTemplateDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetUserAccessibleTemplateQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    /// <summary>
    /// A template can be read by the Secretariat, by heads at a level that fills it, and by anyone who
    /// can see a report filled on it (so observers can read their child units' reports).
    /// </summary>
    public async Task<Result<ReportTemplateDto>> Handle(GetUserAccessibleTemplateQuery request, CancellationToken cancellationToken)
    {
        var template = await _context.ReportTemplates
            .Include(t => t.ReportTypeEntity)
            .Include(rt => rt.Sections)
                .ThenInclude(rs => rs.Questions)
            .AsNoTracking()
            .FirstOrDefaultAsync(rt => rt.Id == request.TemplateId, cancellationToken);

        if (template == null)
        {
            return Result<ReportTemplateDto>.Failure("Report template not found");
        }

        var allowed = ReportingScope.IsSecretariat(_currentUser)
            || template.IsForAllMembers
            || template.IsFilledBy(_currentUser.OrganizationLevel)
            || await _context.ReportSubmissions.VisibleTo(_currentUser).AnyAsync(s => s.ReportTemplateId == template.Id, cancellationToken);

        if (!allowed)
        {
            return Result<ReportTemplateDto>.Failure("User does not have permission to access this template");
        }

        var templateDto = new ReportTemplateDto
        {
            Id = template.Id,
            ReportTypeId = template.ReportTypeId,
            Version = template.Version,
            VersionGroupId = template.RootTemplateId ?? template.Id,
            PublishedAt = template.PublishedAt,
            VersionStatus = template.IsDraft ? "Draft" : template.IsActive ? "Current" : "Retired",
            Levels = template.SubmittingLevels.ToList(),
            QuestionCount = template.Sections.Sum(sec => sec.Questions.Count),
            RequiredQuestionCount = template.Sections.Sum(sec => sec.Questions.Count(q => q.IsRequired)),
            ReportTypeCode = template.ReportTypeEntity != null ? template.ReportTypeEntity.Code : null,
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