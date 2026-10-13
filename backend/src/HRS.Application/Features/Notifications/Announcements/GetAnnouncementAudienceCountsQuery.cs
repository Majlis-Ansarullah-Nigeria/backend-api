using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Notifications;
using Mediator;

namespace HRS.Application.Features.Notifications.Announcements;

public sealed record AnnouncementAudienceCount(AnnouncementAudience Audience, int RecipientCount);

/// <summary>Live counts for the "Send to" dropdown (the mockup's "All Muqam Heads (214)").</summary>
public sealed record GetAnnouncementAudienceCountsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<AnnouncementAudienceCount>>>;

public sealed class GetAnnouncementAudienceCountsQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetAnnouncementAudienceCountsQuery, Result<IReadOnlyList<AnnouncementAudienceCount>>>
{
    private static readonly AnnouncementAudience[] AllAudiences = Enum.GetValues<AnnouncementAudience>();

    public async ValueTask<Result<IReadOnlyList<AnnouncementAudienceCount>>> Handle(
        GetAnnouncementAudienceCountsQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<AnnouncementAudienceCount>>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat can send announcements."));
        }

        var now = clock.UtcNow;
        var counts = new List<AnnouncementAudienceCount>();

        foreach (var audience in AllAudiences)
        {
            var recipients = await AnnouncementAudienceResolver.ResolveRecipientUserIdsAsync(db, audience, now, cancellationToken);
            counts.Add(new AnnouncementAudienceCount(audience, recipients.Count));
        }

        return Result.Success<IReadOnlyList<AnnouncementAudienceCount>>(counts);
    }
}
