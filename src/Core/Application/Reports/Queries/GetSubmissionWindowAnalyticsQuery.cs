using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetSubmissionWindowAnalyticsQuery(Guid WindowId) : IRequest<Result<SubmissionWindowAnalyticsDto>>;

public class GetSubmissionWindowAnalyticsQueryHandler : IRequestHandler<GetSubmissionWindowAnalyticsQuery, Result<SubmissionWindowAnalyticsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSubmissionWindowAnalyticsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SubmissionWindowAnalyticsDto>> Handle(GetSubmissionWindowAnalyticsQuery request, CancellationToken cancellationToken)
    {
        // Get the submission window
        var window = await _context.SubmissionWindows
            .FirstOrDefaultAsync(sw => sw.Id == request.WindowId, cancellationToken);

        if (window == null)
        {
            return Result<SubmissionWindowAnalyticsDto>.Failure("Submission window not found");
        }

        // Get submissions for this window
        var submissions = await _context.ReportSubmissions
            .Where(rs => rs.SubmissionWindowId == request.WindowId)
            .ToListAsync(cancellationToken);

        // Calculate analytics
        var totalSubmissions = submissions.Count;
        var approvedSubmissions = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Approved);
        var rejectedSubmissions = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Rejected);
        var submittedSubmissions = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Submitted);
        var draftSubmissions = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Draft);

        var approvalRate = totalSubmissions > 0 ? (double)approvedSubmissions / totalSubmissions * 100 : 0;

        // Daily trend data
        var dailyTrend = submissions
            .GroupBy(s => s.CreatedOn.Date)
            .Select(g => new DailyTrendDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Count = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        // Organization participation
        var organizationParticipation = new List<OrganizationParticipationDto>();
        if (submissions.Any(s => s.MuqamId.HasValue))
        {
            var muqamSubmissions = submissions.Where(s => s.MuqamId.HasValue)
                .GroupBy(s => s.MuqamId)
                .Select(g => new OrganizationParticipationDto
                {
                    Name = $"Muqam {g.Key}",
                    Count = g.Count()
                });
            organizationParticipation.AddRange(muqamSubmissions);
        }

        if (submissions.Any(s => s.DilaId.HasValue))
        {
            var dilaSubmissions = submissions.Where(s => s.DilaId.HasValue)
                .GroupBy(s => s.DilaId)
                .Select(g => new OrganizationParticipationDto
                {
                    Name = $"Dila {g.Key}",
                    Count = g.Count()
                });
            organizationParticipation.AddRange(dilaSubmissions);
        }

        if (submissions.Any(s => s.ZoneId.HasValue))
        {
            var zoneSubmissions = submissions.Where(s => s.ZoneId.HasValue)
                .GroupBy(s => s.ZoneId)
                .Select(g => new OrganizationParticipationDto
                {
                    Name = $"Zone {g.Key}",
                    Count = g.Count()
                });
            organizationParticipation.AddRange(zoneSubmissions);
        }

        var analyticsDto = new SubmissionWindowAnalyticsDto
        {
            TotalSubmissions = totalSubmissions,
            ApprovedSubmissions = approvedSubmissions,
            RejectedSubmissions = rejectedSubmissions,
            SubmittedSubmissions = submittedSubmissions,
            DraftSubmissions = draftSubmissions,
            ApprovalRate = approvalRate,
            DailyTrend = dailyTrend,
            OrganizationParticipation = organizationParticipation,
            AverageProcessingTime = 0 // Placeholder - would need to calculate based on submission/approval times
        };

        return Result<SubmissionWindowAnalyticsDto>.Success(analyticsDto);
    }
}

public class SubmissionWindowAnalyticsDto
{
    public int TotalSubmissions { get; set; }
    public int ApprovedSubmissions { get; set; }
    public int RejectedSubmissions { get; set; }
    public int SubmittedSubmissions { get; set; }
    public int DraftSubmissions { get; set; }
    public double ApprovalRate { get; set; }
    public double AverageProcessingTime { get; set; } // in days
    public List<DailyTrendDto> DailyTrend { get; set; } = new();
    public List<OrganizationParticipationDto> OrganizationParticipation { get; set; } = new();
}

public class DailyTrendDto
{
    public string Date { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class OrganizationParticipationDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}