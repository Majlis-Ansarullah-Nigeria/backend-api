using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetOverdueSubmissionsQuery : IRequest<Result<List<OverdueSubmissionDto>>>;

public class GetOverdueSubmissionsQueryHandler : IRequestHandler<GetOverdueSubmissionsQuery, Result<List<OverdueSubmissionDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetOverdueSubmissionsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<OverdueSubmissionDto>>> Handle(GetOverdueSubmissionsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Get all open/expired submission windows
        var overdueWindows = await _context.SubmissionWindows
            .Include(w => w.ReportTemplate)
            .Where(w => w.IsActive && w.EndDate < now)
            .OrderBy(w => w.EndDate)
            .ToListAsync(cancellationToken);

        var result = new List<OverdueSubmissionDto>();

        foreach (var window in overdueWindows)
        {
            // Get all submissions for this window
            var submittedSubmissions = await _context.ReportSubmissions
                .Where(s => s.SubmissionWindowId == window.Id)
                .Select(s => new
                {
                    s.MuqamId,
                    s.DilaId,
                    s.ZoneId,
                    s.OrganizationLevel
                })
                .ToListAsync(cancellationToken);

            // Get all organizations that should have submitted based on the template's organization level
            var targetLevel = window.ReportTemplate.OrganizationLevel;

            // Calculate missing submissions based on organization level
            int expectedCount = 0;
            int submittedCount = submittedSubmissions.Count;
            List<string> missingOrganizations = new();
            List<MissingOrganizationDetailDto> missingOrgDetails = new();

            switch (targetLevel)
            {
                case Domain.Enums.OrganizationLevel.Muqam:
                    var allMuqams = await _context.Muqams.ToListAsync(cancellationToken);
                    expectedCount = allMuqams.Count;
                    var submittedMuqamIds = submittedSubmissions.Select(s => s.MuqamId).ToHashSet();
                    var missingMuqams = allMuqams.Where(m => !submittedMuqamIds.Contains(m.Id)).ToList();
                    missingOrganizations = missingMuqams.Select(m => m.Name).ToList();
                    missingOrgDetails = missingMuqams.Select(m => new MissingOrganizationDetailDto
                    {
                        OrganizationId = m.Id,
                        OrganizationName = m.Name,
                        ContactPerson = m.ContactPerson,
                        PhoneNumber = m.PhoneNumber,
                        Email = m.Email,
                        Address = m.Address
                    }).ToList();
                    break;

                case Domain.Enums.OrganizationLevel.Dila:
                    var allDilas = await _context.Dilas.ToListAsync(cancellationToken);
                    expectedCount = allDilas.Count;
                    var submittedDilaIds = submittedSubmissions.Select(s => s.DilaId).ToHashSet();
                    var missingDilas = allDilas.Where(d => !submittedDilaIds.Contains(d.Id)).ToList();
                    missingOrganizations = missingDilas.Select(d => d.Name).ToList();
                    missingOrgDetails = missingDilas.Select(d => new MissingOrganizationDetailDto
                    {
                        OrganizationId = d.Id,
                        OrganizationName = d.Name,
                        ContactPerson = d.ContactPerson,
                        PhoneNumber = d.PhoneNumber,
                        Email = d.Email,
                        Address = d.Address
                    }).ToList();
                    break;

                case Domain.Enums.OrganizationLevel.Zone:
                    var allZones = await _context.Zones.ToListAsync(cancellationToken);
                    expectedCount = allZones.Count;
                    var submittedZoneIds = submittedSubmissions.Select(s => s.ZoneId).ToHashSet();
                    var missingZones = allZones.Where(z => !submittedZoneIds.Contains(z.Id)).ToList();
                    missingOrganizations = missingZones.Select(z => z.Name).ToList();
                    missingOrgDetails = missingZones.Select(z => new MissingOrganizationDetailDto
                    {
                        OrganizationId = z.Id,
                        OrganizationName = z.Name,
                        ContactPerson = z.ContactPerson,
                        PhoneNumber = z.PhoneNumber,
                        Email = z.Email,
                        Address = z.Address
                    }).ToList();
                    break;

                case Domain.Enums.OrganizationLevel.National:
                    // National level reports - expect 1
                    expectedCount = 1;
                    if (submittedCount == 0)
                    {
                        missingOrganizations.Add("National Secretariat");
                        missingOrgDetails.Add(new MissingOrganizationDetailDto
                        {
                            OrganizationId = Guid.Empty,
                            OrganizationName = "National Secretariat",
                            ContactPerson = null,
                            PhoneNumber = null,
                            Email = null,
                            Address = null
                        });
                    }
                    break;

                case Domain.Enums.OrganizationLevel.Jamaat:
                default:
                    // For Jamaat-level or other levels, we can't determine expected count easily
                    // Skip this for now or implement Jamaat tracking separately
                    expectedCount = submittedCount; // Avoid showing as overdue
                    break;
            }

            var daysOverdue = (int)(now - window.EndDate).TotalDays;

            var overdueDto = new OverdueSubmissionDto
            {
                SubmissionWindowId = window.Id,
                WindowName = window.Name,
                TemplateId = window.ReportTemplateId,
                TemplateName = window.ReportTemplate.Name,
                EndDate = window.EndDate,
                DaysOverdue = daysOverdue,
                ExpectedSubmissions = expectedCount,
                ReceivedSubmissions = submittedCount,
                MissingSubmissions = expectedCount - submittedCount,
                MissingOrganizations = missingOrganizations,
                MissingOrganizationDetails = missingOrgDetails,
                ComplianceRate = expectedCount > 0 ? (double)submittedCount / expectedCount * 100 : 0
            };

            // Only include if there are missing submissions
            if (overdueDto.MissingSubmissions > 0)
            {
                result.Add(overdueDto);
            }
        }

        return Result<List<OverdueSubmissionDto>>.Success(result);
    }
}

public record OverdueSubmissionDto
{
    public Guid SubmissionWindowId { get; init; }
    public string WindowName { get; init; } = default!;
    public Guid TemplateId { get; init; }
    public string TemplateName { get; init; } = default!;
    public DateTime EndDate { get; init; }
    public int DaysOverdue { get; init; }
    public int DaysRemaining => DaysOverdue > 0 ? 0 : -DaysOverdue; // For windows not yet overdue
    public int ExpectedSubmissions { get; init; }
    public int ReceivedSubmissions { get; init; }
    public int MissingSubmissions { get; init; }
    public List<string> MissingOrganizations { get; init; } = new();
    public List<MissingOrganizationDetailDto> MissingOrganizationDetails { get; init; } = new();
    public double ComplianceRate { get; init; }
}

public record MissingOrganizationDetailDto
{
    public Guid OrganizationId { get; init; }
    public string OrganizationName { get; init; } = default!;
    public string? ContactPerson { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
}
