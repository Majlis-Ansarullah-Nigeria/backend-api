using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
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
    private readonly UserManager<ApplicationUser> _userManager;

    public GetUserAccessibleTemplateQueryHandler(
        IApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<Result<ReportTemplateDto>> Handle(GetUserAccessibleTemplateQuery request, CancellationToken cancellationToken)
    {
        var template = await _context.ReportTemplates
            .Include(rt => rt.Sections)
                .ThenInclude(rs => rs.Questions)
            .FirstOrDefaultAsync(rt => rt.Id == request.TemplateId, cancellationToken);

        if (template == null)
        {
            return Result<ReportTemplateDto>.Failure("Report template not found");
        }

        // Get the current user's information
        var currentUserIdString = request.UserId?.ToString() ?? "00000000-0000-0000-0000-000000000000";
        if (!Guid.TryParse(currentUserIdString, out Guid currentUserId))
        {
            return Result<ReportTemplateDto>.Failure("Invalid user ID");
        }

        var currentUser = await _userManager.FindByIdAsync(currentUserId.ToString());

        if (currentUser == null)
        {
            return Result<ReportTemplateDto>.Failure("User not found");
        }

        // Get user's roles using UserManager
        var userRoles = await _userManager.GetRolesAsync(currentUser);

        // Check if user can access this template based on organization level
        if (!CanUserAccessTemplate(userRoles.ToList(), template.OrganizationLevel, template.IsForAllMembers))
        {
            return Result<ReportTemplateDto>.Failure("User does not have permission to access this template");
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

    private bool CanUserAccessTemplate(List<string> userRoles, OrganizationLevel templateOrgLevel, bool isForAllMembers)
    {
        // If template is for all members, anyone with a member role can access
        if (isForAllMembers)
        {
            return userRoles.Any(role => role.ToLower().Contains("member"));
        }

        // Map organization levels to required role keywords
        var requiredKeywords = templateOrgLevel switch
        {
            OrganizationLevel.Muqam => new[] { "zaim", "muqam", "head" },
            OrganizationLevel.Dila => new[] { "nazim", "dila", "manager" },
            OrganizationLevel.Zone => new[] { "zonal", "zone", "coordinator" },
            OrganizationLevel.National => new[] { "national", "sadr", "president", "admin" },
            _ => new string[0]
        };

        // Check if user has any of the required role keywords
        return userRoles.Any(userRole =>
            requiredKeywords.Any(keyword =>
                userRole.ToLower().Contains(keyword.ToLower())));
    }
}