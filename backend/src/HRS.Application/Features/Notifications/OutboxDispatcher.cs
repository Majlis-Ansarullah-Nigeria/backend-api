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
    private const int MaxAttempts = 5;

    public static async Task<int> RunAsync(
        IAppDbContext db, IIdentityService identityService, IEmailSender emailSender, IClock clock, CancellationToken cancellationToken)
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

        var templatesByEventType = await db.NotificationTemplates.AsNoTracking()
            .ToDictionaryAsync(t => t.EventType, cancellationToken);

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

            try
            {
                var (subject, body) = NotificationTemplateRenderer.Render(template, placeholders);
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
