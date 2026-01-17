using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetAnalyticsQuery(Guid? TemplateId = null, Guid? MuqamId = null) : IRequest<ReportAnalyticsDto>;

public record ReportAnalyticsDto
{
    public int TotalSubmissions { get; init; }
    public int PendingApprovals { get; init; }
    public int ApprovedSubmissions { get; init; }
    public int RejectedSubmissions { get; init; }
    public int DraftSubmissions { get; init; }
    public decimal CompletionRate { get; init; }
    public Dictionary<string, int> SubmissionsByTemplate { get; init; } = new();
    public Dictionary<string, int> SubmissionsByMuqam { get; init; } = new();
    public Dictionary<string, int> SubmissionsByMonth { get; init; } = new();
}

public class GetAnalyticsQueryHandler : IRequestHandler<GetAnalyticsQuery, ReportAnalyticsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAnalyticsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ReportAnalyticsDto> Handle(GetAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ReportSubmissions
            .Include(s => s.ReportTemplate)
            .AsQueryable();

        // Filter by organization level
        var orgLevel = _currentUser.OrganizationLevel;

        switch (orgLevel)
        {
            case OrganizationLevel.Muqam:
                if (_currentUser.MuqamId.HasValue)
                {
                    query = query.Where(s => s.MuqamId == _currentUser.MuqamId.Value);
                }
                break;

            case OrganizationLevel.Dila:
                if (_currentUser.DilaId.HasValue)
                {
                    query = query.Where(s => s.DilaId == _currentUser.DilaId.Value);
                }
                break;

            case OrganizationLevel.Zone:
                if (_currentUser.ZoneId.HasValue)
                {
                    query = query.Where(s => s.ZoneId == _currentUser.ZoneId.Value);
                }
                break;

            case OrganizationLevel.National:
                // National level - no filtering
                break;
        }

        // Filter by template if specified
        if (request.TemplateId.HasValue)
        {
            query = query.Where(s => s.ReportTemplateId == request.TemplateId.Value);
        }

        // Filter by muqam if specified
        if (request.MuqamId.HasValue)
        {
            query = query.Where(s => s.MuqamId == request.MuqamId.Value);
        }

        var submissions = await query.ToListAsync(cancellationToken);

        var totalSubmissions = submissions.Count;
        var pendingApprovals = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Submitted);
        var approvedSubmissions = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Approved);
        var rejectedSubmissions = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Rejected);
        var draftSubmissions = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Draft);

        var completionRate = totalSubmissions > 0
            ? Math.Round((decimal)approvedSubmissions / totalSubmissions * 100, 2)
            : 0;

        var submissionsByTemplate = submissions
            .Where(s => s.ReportTemplate != null)
            .GroupBy(s => s.ReportTemplate!.Name)
            .ToDictionary(g => g.Key, g => g.Count());

        // Get Muqam names for submissions
        var muqamIds = submissions.Where(s => s.MuqamId.HasValue).Select(s => s.MuqamId!.Value).Distinct().ToList();
        var muqams = await _context.Muqams
            .Where(m => muqamIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);

        var submissionsByMuqam = submissions
            .Where(s => s.MuqamId.HasValue && muqams.ContainsKey(s.MuqamId.Value))
            .GroupBy(s => muqams[s.MuqamId!.Value])
            .ToDictionary(g => g.Key, g => g.Count());

        var submissionsByMonth = submissions
            .Where(s => s.SubmittedAt.HasValue)
            .GroupBy(s => s.SubmittedAt!.Value.ToString("MMM yyyy"))
            .ToDictionary(g => g.Key, g => g.Count());

        return new ReportAnalyticsDto
        {
            TotalSubmissions = totalSubmissions,
            PendingApprovals = pendingApprovals,
            ApprovedSubmissions = approvedSubmissions,
            RejectedSubmissions = rejectedSubmissions,
            DraftSubmissions = draftSubmissions,
            CompletionRate = completionRate,
            SubmissionsByTemplate = submissionsByTemplate,
            SubmissionsByMuqam = submissionsByMuqam,
            SubmissionsByMonth = submissionsByMonth
        };
    }
}
