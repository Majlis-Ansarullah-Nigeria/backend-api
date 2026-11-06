using ManagementApi.Application.Reports.Common;
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
    private readonly ICurrentUserService _currentUser;

    public GetSubmissionWindowAnalyticsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
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

        // Get submissions for this window with approvals
        var submissions = await _context.ReportSubmissions
            .VisibleTo(_currentUser)
            .Where(rs => rs.SubmissionWindowId == request.WindowId)
            .Include(rs => rs.Approvals)
            .ToListAsync(cancellationToken);

        // Calculate analytics
        var totalSubmissions = submissions.Count;
        var acknowledgedSubmissions = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Acknowledged);
        var returnedSubmissions = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Returned);
        var submittedSubmissions = submissions.Count(s => s.Status is Domain.Enums.SubmissionStatus.Submitted or Domain.Enums.SubmissionStatus.InReview);
        var draftSubmissions = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Draft);

        var acknowledgementRate = totalSubmissions > 0 ? (double)acknowledgedSubmissions / totalSubmissions * 100 : 0;

        // US-24: Calculate average processing time (time from submission to approval/rejection)
        double averageProcessingTime = 0;
        var processedSubmissions = submissions
            .Where(s => s.Status == Domain.Enums.SubmissionStatus.Acknowledged ||
                       s.Status == Domain.Enums.SubmissionStatus.Returned)
            .Where(s => s.Approvals != null && s.Approvals.Any())
            .ToList();

        if (processedSubmissions.Any())
        {
            var processingTimes = new List<double>();

            foreach (var submission in processedSubmissions)
            {
                // Get the submission date (when status changed from Draft to Submitted)
                // Use SubmittedAt or fallback to LastModifiedOn/CreatedOn
                var submissionDate = submission.SubmittedAt ?? submission.LastModifiedOn ?? submission.CreatedOn;

                // Get the latest approval/rejection date
                var latestApproval = submission.Approvals
                    .OrderByDescending(a => a.ActionDate)
                    .FirstOrDefault();

                if (latestApproval != null)
                {
                    var processingTime = (latestApproval.ActionDate - submissionDate).TotalDays;
                    if (processingTime >= 0) // Only include positive values
                    {
                        processingTimes.Add(processingTime);
                    }
                }
            }

            if (processingTimes.Any())
            {
                averageProcessingTime = Math.Round(processingTimes.Average(), 2);
            }
        }

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
            AcknowledgedSubmissions = acknowledgedSubmissions,
            ReturnedSubmissions = returnedSubmissions,
            SubmittedSubmissions = submittedSubmissions,
            DraftSubmissions = draftSubmissions,
            AcknowledgementRate = acknowledgementRate,
            DailyTrend = dailyTrend,
            OrganizationParticipation = organizationParticipation,
            AverageProcessingTime = averageProcessingTime // US-24: Calculated average time from submission to approval/rejection (in days)
        };

        return Result<SubmissionWindowAnalyticsDto>.Success(analyticsDto);
    }
}

public class SubmissionWindowAnalyticsDto
{
    public int TotalSubmissions { get; set; }
    public int AcknowledgedSubmissions { get; set; }
    public int ReturnedSubmissions { get; set; }
    public int SubmittedSubmissions { get; set; }
    public int DraftSubmissions { get; set; }
    public double AcknowledgementRate { get; set; }
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