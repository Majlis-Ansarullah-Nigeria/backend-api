using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

/// <summary>Counts for the review inbox tabs, limited to the reports the user may see.</summary>
public record GetInboxCountsQuery(Guid? WindowId) : IRequest<InboxCountsDto>;

public record InboxCountsDto(int ToReview, int Returned, int Acknowledged, int AllSubmitted);

public class GetInboxCountsQueryHandler : IRequestHandler<GetInboxCountsQuery, InboxCountsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetInboxCountsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<InboxCountsDto> Handle(GetInboxCountsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ReportSubmissions.AsNoTracking().VisibleTo(_currentUser);
        if (request.WindowId.HasValue)
        {
            query = query.Where(s => s.SubmissionWindowId == request.WindowId.Value);
        }

        var byStatus = await query
            .GroupBy(s => s.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        int Count(params SubmissionStatus[] statuses) => statuses.Sum(s => byStatus.GetValueOrDefault(s));

        return new InboxCountsDto(
            ToReview: Count(SubmissionStatus.Submitted, SubmissionStatus.InReview),
            Returned: Count(SubmissionStatus.Returned),
            Acknowledged: Count(SubmissionStatus.Acknowledged),
            AllSubmitted: Count(SubmissionStatus.Submitted, SubmissionStatus.InReview, SubmissionStatus.Returned, SubmissionStatus.Acknowledged));
    }
}
