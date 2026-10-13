using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Options;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Notifications;
using Mediator;
using Microsoft.Extensions.Options;

namespace HRS.Application.Features.Notifications.Announcements;

public sealed record SendAnnouncementCommand(
    Guid UserId, string Title, string Message, AnnouncementAudience Audience, bool ShowAsBanner, bool SendEmail)
    : IRequest<Result<AnnouncementSummary>>;

public sealed class SendAnnouncementCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IOptions<AppOptions> appOptions)
    : IRequestHandler<SendAnnouncementCommand, Result<AnnouncementSummary>>
{
    public async ValueTask<Result<AnnouncementSummary>> Handle(SendAnnouncementCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<AnnouncementSummary>(Error.Forbidden("FORBIDDEN", "Only the Secretariat can send announcements."));
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
        {
            return Result.Failure<AnnouncementSummary>(Error.Validation("VALIDATION", "Title and message are required."));
        }

        var now = clock.UtcNow;
        var recipientUserIds = await AnnouncementAudienceResolver.ResolveRecipientUserIdsAsync(db, request.Audience, now, cancellationToken);

        var announcement = Announcement.Create(
            Guid.CreateVersion7(), request.Title.Trim(), request.Message.Trim(), request.Audience, request.ShowAsBanner, request.SendEmail,
            request.UserId, recipientUserIds.Count, now);
        db.Announcements.Add(announcement);

        var placeholders = new Dictionary<string, string>
        {
            ["title"] = announcement.Title,
            ["message"] = announcement.Message,
            ["link"] = $"{appOptions.Value.PublicUrl.TrimEnd('/')}/home",
            ["isBanner"] = announcement.ShowAsBanner ? "true" : "false",
            ["sendEmail"] = announcement.SendEmail ? "true" : "false",
        };

        foreach (var recipientUserId in recipientUserIds)
        {
            OutboxWriter.EnqueueForUser(db, recipientUserId, NotificationEventType.Announcement, placeholders, now);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new AnnouncementSummary(
            announcement.Id, announcement.Title, announcement.Message, announcement.Audience,
            announcement.ShowAsBanner, announcement.SendEmail, announcement.DeliveredCount, announcement.CreatedAt));
    }
}
