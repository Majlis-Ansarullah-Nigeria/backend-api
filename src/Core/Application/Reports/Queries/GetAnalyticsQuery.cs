using ManagementApi.Application.Reports.Common;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetAnalyticsQuery(Guid? TemplateId = null, Guid? MuqamId = null) : IRequest<ReportAnalyticsDto>;

public record ReportAnalyticsDto
{
    public int TotalSubmissions { get; init; }
    public int PendingReview { get; init; }
    public int AcknowledgedSubmissions { get; init; }
    public int ReturnedSubmissions { get; init; }
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

        // SECURITY: only reports the current user may see
        query = query.VisibleTo(_currentUser);

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
        var pendingReview = submissions.Count(s => s.Status is Domain.Enums.SubmissionStatus.Submitted or Domain.Enums.SubmissionStatus.InReview);
        var acknowledgedSubmissions = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Acknowledged);
        var returnedSubmissions = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Returned);
        var draftSubmissions = submissions.Count(s => s.Status == Domain.Enums.SubmissionStatus.Draft);

        var completionRate = totalSubmissions > 0
            ? Math.Round((decimal)acknowledgedSubmissions / totalSubmissions * 100, 2)
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
            PendingReview = pendingReview,
            AcknowledgedSubmissions = acknowledgedSubmissions,
            ReturnedSubmissions = returnedSubmissions,
            DraftSubmissions = draftSubmissions,
            CompletionRate = completionRate,
            SubmissionsByTemplate = submissionsByTemplate,
            SubmissionsByMuqam = submissionsByMuqam,
            SubmissionsByMonth = submissionsByMonth
        };
    }
}
