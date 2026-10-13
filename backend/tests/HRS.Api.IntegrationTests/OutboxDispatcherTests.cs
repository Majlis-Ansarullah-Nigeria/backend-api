using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Notifications;
using HRS.Domain.Notifications;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>M8.1's dispatcher mechanics — rendering, the auto-supplied {{headName}}, skipping an
/// already-processed row, and the retry cap. The retrofitted trigger tests elsewhere (Inbox,
/// Windows, Templates, ReportSubmission, ReportComments) already exercise the full
/// write-outbox -&gt; dispatch -&gt; email round trip for each real event; these tests isolate the
/// dispatcher itself by writing OutboxMessage rows directly.
///
/// Every assertion here is scoped to this test's own freshly-created recipient — every test in
/// HRS.Api.IntegrationTests shares one Testcontainers Postgres that's never reset between tests,
/// and WindowJobRunnerTests' own broadcast-scope windows (no explicit TargetUnitIds) mean the
/// outbox can carry a real backlog of other tests' pending messages by the time this class runs.
/// DispatchAsync's own return value (how many it sent *this call, across the whole table*) is
/// therefore never asserted on directly — only this test's own row, by its own recipient id.</summary>
[Collection(ApiTestCollection.Name)]
public class OutboxDispatcherTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<(Guid UserId, string Email, string Name)> CreateUserAsync(string name = "Test User")
    {
        var email = UniqueEmail();
        var userId = await factory.CreateUserAsync(email, "Correct-Horse-Outbox1", name: name);
        return (userId, email, name);
    }

    private async Task EnqueueAsync(Guid recipientUserId, NotificationEventType eventType, IReadOnlyDictionary<string, string> placeholders)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        OutboxWriter.EnqueueForUser(db, recipientUserId, eventType, placeholders, clock.UtcNow);
        await db.SaveChangesAsync();
    }

    private async Task DispatchOnceAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        await OutboxDispatcher.RunAsync(db, identityService, factory.EmailSender, clock, CancellationToken.None);
    }

    private async Task<OutboxMessage> GetOnlyMessageForAsync(Guid recipientUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.RecipientUserId == recipientUserId);
    }

    /// <summary>Keeps calling the dispatcher until this specific message leaves the "still
    /// pending, might still be retried" state — tolerates an arbitrarily large backlog of other
    /// tests' messages sitting ahead of it in the batch (oldest-first, BatchSize per call).</summary>
    private async Task<OutboxMessage> DispatchUntilSettledAsync(Guid recipientUserId, int maxAttempts = 5000)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            var current = await GetOnlyMessageForAsync(recipientUserId);
            if (current.ProcessedAt is not null || current.Attempts > 0)
            {
                return current;
            }

            await DispatchOnceAsync();
        }

        throw new InvalidOperationException($"Message for {recipientUserId} never settled after {maxAttempts} dispatcher runs.");
    }

    [Fact]
    public async Task HeadNameIsAutoSuppliedFromTheRecipientsCurrentNameEvenWhenNotInThePayload()
    {
        var (userId, email, name) = await CreateUserAsync("Amina Bello");
        await EnqueueAsync(userId, NotificationEventType.ReportAcknowledged, new Dictionary<string, string>
        {
            ["unitName"] = "Dila Ibafo",
            ["reportTypeName"] = "Monthly Report",
            ["periodLabel"] = "September 2026",
            ["link"] = "http://localhost:3000/reports/1/edit",
        });

        var settled = await DispatchUntilSettledAsync(userId);

        Assert.NotNull(settled.ProcessedAt);
        var sent = factory.EmailSender.LastSentTo(email);
        Assert.NotNull(sent);
        Assert.Contains(name, sent!.TextBody);
    }

    [Fact]
    public async Task ADispatchedMessageIsMarkedProcessedAndNeverResent()
    {
        var (userId, email, _) = await CreateUserAsync();
        await EnqueueAsync(userId, NotificationEventType.ReportAcknowledged, new Dictionary<string, string>
        {
            ["unitName"] = "Dila Ibafo",
            ["reportTypeName"] = "Monthly Report",
            ["periodLabel"] = "Sep 2026",
            ["link"] = "http://x",
        });

        var afterFirst = await DispatchUntilSettledAsync(userId);
        Assert.NotNull(afterFirst.ProcessedAt);
        Assert.NotNull(factory.EmailSender.LastSentTo(email));

        // A few more dispatcher runs must never touch an already-processed message again.
        for (var i = 0; i < 3; i++)
        {
            await DispatchOnceAsync();
        }

        var stillSettled = await GetOnlyMessageForAsync(userId);
        Assert.Equal(afterFirst.ProcessedAt, stillSettled.ProcessedAt);
        Assert.Equal(0, stillSettled.Attempts);
    }

    [Fact]
    public async Task AMissingRecipientRecordsAFailureAndRetriesUpToTheCapThenStops()
    {
        var nonexistentUserId = Guid.CreateVersion7();
        await EnqueueAsync(nonexistentUserId, NotificationEventType.ReportAcknowledged, new Dictionary<string, string>
        {
            ["unitName"] = "Ghost Unit",
            ["reportTypeName"] = "Monthly Report",
            ["periodLabel"] = "Sep 2026",
            ["link"] = "http://x",
        });

        // This recipient can never resolve, so it never "settles" via ProcessedAt — keep
        // dispatching (tolerating however large a backlog of other tests' messages sits ahead of
        // it) until this message's own Attempts reaches the cap.
        OutboxMessage current;
        var safetyCap = 0;
        do
        {
            await DispatchOnceAsync();
            current = await GetOnlyMessageForAsync(nonexistentUserId);
            safetyCap++;
        }
        while (current.Attempts < 5 && safetyCap < 5000);

        var afterFiveAttempts = current;
        Assert.Equal(5, afterFiveAttempts.Attempts);
        Assert.Null(afterFiveAttempts.ProcessedAt);
        Assert.Equal("Recipient account no longer exists.", afterFiveAttempts.LastError);

        await DispatchOnceAsync();
        var afterOneMore = await GetOnlyMessageForAsync(nonexistentUserId);

        Assert.Equal(5, afterOneMore.Attempts); // not picked up again once Attempts reaches the cap.
    }
}
