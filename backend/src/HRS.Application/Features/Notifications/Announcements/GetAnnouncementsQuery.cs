using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.Announcements;

/// <summary>The mockup's "Sent" history — newest first, no pagination (same "kept deliberately
/// simple" call as the notification centre list itself, M8.2).</summary>
public sealed record GetAnnouncementsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<AnnouncementSummary>>>;

public sealed class GetAnnouncementsQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<GetAnnouncementsQuery, Result<IReadOnlyList<AnnouncementSummary>>>
{
    private const int MaxResults = 50;

    public async ValueTask<Result<IReadOnlyList<AnnouncementSummary>>> Handle(GetAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<AnnouncementSummary>>(Error.Forbidden("FORBIDDEN", "Only the Secretariat can see sent announcements."));
        }

        var announcements = await db.Announcements.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(MaxResults)
            .Select(a => new AnnouncementSummary(a.Id, a.Title, a.Message, a.Audience, a.ShowAsBanner, a.SendEmail, a.DeliveredCount, a.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AnnouncementSummary>>(announcements);
    }
}
