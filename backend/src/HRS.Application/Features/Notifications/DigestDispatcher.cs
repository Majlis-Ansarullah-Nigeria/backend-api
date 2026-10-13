using System.Globalization;
using System.Text;
using HRS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications;

/// <summary>FR-NOT-004's "daily digest instead of single emails" — one combined email per
/// digest-opted-in user, covering every Notification row created since their own last digest
/// (OutboxDispatcher already created these rows in real time; digest mode only ever changes the
/// *email* delivery, never suppresses the in-app row). Same split as OutboxDispatcher/
/// WindowJobRunner: pure, testable logic, a thin Worker job adapts it to a recurring schedule.</summary>
public static class DigestDispatcher
{
    public static async Task<int> RunAsync(IAppDbContext db, IIdentityService identityService, IEmailSender emailSender, IClock clock, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var digestUsers = await db.UserNotificationSettings
            .Where(s => s.DigestEnabled)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var settings in digestUsers)
        {
            var since = settings.LastDigestSentAt ?? now.AddDays(-1);
            var notifications = await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == settings.UserId && n.CreatedAt > since && n.CreatedAt <= now)
                .OrderBy(n => n.CreatedAt)
                .ToListAsync(cancellationToken);

            if (notifications.Count == 0)
            {
                settings.MarkDigestSent(now);
                continue;
            }

            var recipient = await identityService.GetUserAsync(settings.UserId, cancellationToken);
            if (recipient is null)
            {
                settings.MarkDigestSent(now);
                continue;
            }

            var body = new StringBuilder();
            body.Append("Hi ").Append(recipient.Name).Append(",\n\nHere's what happened since your last digest:\n\n");
            foreach (var notification in notifications)
            {
                body.Append("- ").Append(notification.Subject);
                if (notification.Link is { } link)
                {
                    body.Append(" (").Append(link).Append(')');
                }

                body.Append('\n');
            }

            var subject = string.Format(CultureInfo.InvariantCulture, "Your daily digest ({0} update{1})", notifications.Count, notifications.Count == 1 ? "" : "s");

            await emailSender.QueueAsync(new EmailMessage(recipient.Email, subject, body.ToString()), cancellationToken);
            settings.MarkDigestSent(now);
            sent++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return sent;
    }
}
