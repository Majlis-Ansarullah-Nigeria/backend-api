using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications;

/// <summary>
/// The "M8's outbox dispatcher" every earlier notification comment in this codebase points at.
/// Pure, testable logic (same split as WindowJobRunner) — the Worker's recurring job is a thin
/// adapter over this. Sends through the existing IEmailSender -&gt; BackgroundEmailDispatcher -&gt;
/// SMTP pipeline (both Api and Worker host that pipeline via AddInfrastructure, so handing off to
/// it here just reuses the same transport, not a second one); the durability win is that a crash
/// between SaveChangesAsync and the actual send no longer loses the notification — the row is
/// still there, unprocessed, for the next run to retry.
/// </summary>
public static class OutboxDispatcher
{
    private const int BatchSize = 100;

    /// <summary>After this many failed attempts, a message is left alone rather than retried
    /// forever — a poison message (e.g. a template that always fails to render) shouldn't spin.</summary>
    public const int MaxAttempts = 5;

    public static async Task<int> RunAsync(
        IAppDbContext db,
        IIdentityService identityService,
        IEmailSender emailSender,
        IPushNotificationSender pushSender,
        INotificationBroadcaster broadcaster,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Attempts < MaxAttempts)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        // FR-ADM-001: every template can say {{organizationName}}.
        var organizationName = await db.OrganizationSettings.AsNoTracking().Select(s => s.Name).FirstOrDefaultAsync(cancellationToken)
            ?? OrganizationBranding.DefaultName;

        var templatesByEventType = await db.NotificationTemplates.AsNoTracking()
            .ToDictionaryAsync(t => t.EventType, cancellationToken);

        var recipientIds = pending.Select(m => m.RecipientUserId).Distinct().ToList();
        var preferencesByUser = await db.NotificationPreferences.AsNoTracking()
            .Where(p => recipientIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var preferenceLookup = preferencesByUser.ToDictionary(p => (p.UserId, p.Category));
        var digestEnabledByUser = await db.UserNotificationSettings.AsNoTracking()
            .Where(s => recipientIds.Contains(s.UserId) && s.DigestEnabled)
            .Select(s => s.UserId)
            .ToHashSetAsync(cancellationToken);

        // Running unread counts per recipient touched in this batch, seeded lazily from the DB
        // (which doesn't yet see this batch's own not-yet-saved Notification rows — SaveChanges
        // only happens once, at the end) and incremented locally as rows are added below.
        var unreadCountsByUser = new Dictionary<Guid, int>();

        var sent = 0;
        foreach (var message in pending)
        {
            if (!templatesByEventType.TryGetValue(message.EventType, out var template))
            {
                message.RecordFailure("No notification template configured for this event type.");
                continue;
            }

            var recipient = await identityService.GetUserAsync(message.RecipientUserId, cancellationToken);
            if (recipient is null)
            {
                message.RecordFailure("Recipient account no longer exists.");
                continue;
            }

            var placeholders = JsonSerializer.Deserialize<Dictionary<string, string>>(message.PayloadJson, JsonSerializerOptions.Web)
                ?? new Dictionary<string, string>();
            placeholders["headName"] = recipient.Name;
            placeholders.TryAdd("organizationName", organizationName);

            string subject;
            string body;
            try
            {
                (subject, body) = NotificationTemplateRenderer.Render(template, placeholders);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.RecordFailure(ex.Message);
                continue;
            }

            var link = placeholders.GetValueOrDefault("link");
            var isBanner = placeholders.GetValueOrDefault("isBanner") == "true";

            // FR-NOT-004: no preference row for this (user, category) means "both channels on"
            // (the Profile mockup's default); events with no category (ChildSubmitted,
            // TemplateChanged — no row drawn for either) always fire both.
            var category = NotificationCategoryMap.For(message.EventType);
            var preference = category is { } c ? preferenceLookup.GetValueOrDefault((message.RecipientUserId, c)) : null;
            var inAppEnabled = preference?.InAppEnabled ?? true;
            var emailEnabled = preference?.EmailEnabled ?? true;

            // In-app notification centre row: created once per outbox message regardless of the
            // email leg's outcome below, and idempotent across re-dispatch (a message already
            // marked sent never reaches this loop again, but a retried *failed* message would).
            // Gates push too — both are the same "immediate, on this device" surface, distinct
            // from email's possible digest deferral below.
            if (inAppEnabled)
            {
                var alreadyNotified = await db.Notifications
                    .AnyAsync(n => n.OutboxMessageId == message.Id, cancellationToken);
                if (!alreadyNotified)
                {
                    db.Notifications.Add(Notification.Create(
                        Guid.CreateVersion7(), message.RecipientUserId, message.EventType, subject, body, link, message.Id, isBanner, now));

                    if (!unreadCountsByUser.TryGetValue(message.RecipientUserId, out var unreadCount))
                    {
                        unreadCount = await db.Notifications
                            .CountAsync(n => n.UserId == message.RecipientUserId && n.ReadAt == null, cancellationToken);
                    }

                    unreadCount++;
                    unreadCountsByUser[message.RecipientUserId] = unreadCount;
                    await broadcaster.NotifyUnreadCountChangedAsync(message.RecipientUserId, unreadCount, cancellationToken);

                    var subscriptions = await db.PushSubscriptions
                        .Where(s => s.UserId == message.RecipientUserId)
                        .ToListAsync(cancellationToken);

                    foreach (var subscription in subscriptions)
                    {
                        try
                        {
                            await pushSender.SendAsync(subscription, new PushPayload(subject, body, link), cancellationToken);
                        }
                        catch (PushSubscriptionGoneException)
                        {
                            db.PushSubscriptions.Remove(subscription);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            // Best-effort: a push failure never affects this outbox message's own
                            // email retry state (Attempts/ProcessedAt below).
                        }
                    }
                }
            }

            // FR-NOT-006: the Secretariat can send an announcement without its email leg at all
            // ("Show as" unchecking Email) — distinct from the recipient's own preference below.
            var announcementWantsEmail = message.EventType != NotificationEventType.Announcement
                || placeholders.GetValueOrDefault("sendEmail") != "false";

            // FR-NOT-004's digest: "instead of single emails" — a digest-opted-in user gets
            // nothing here (DigestRecurringJob batches their Notification rows separately);
            // skipping is a deliberate, successful outcome for this message's email leg, not a
            // failure to retry. Same for a category the user has turned email off for.
            if (!emailEnabled || !announcementWantsEmail || digestEnabledByUser.Contains(message.RecipientUserId))
            {
                message.MarkSent(now);
                continue;
            }

            try
            {
                await emailSender.QueueAsync(new EmailMessage(recipient.Email, subject, body), cancellationToken);
                message.MarkSent(now);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.RecordFailure(ex.Message);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return sent;
    }
}
