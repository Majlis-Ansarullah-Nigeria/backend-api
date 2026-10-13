namespace HRS.Domain.Notifications;

/// <summary>
/// A browser's Web Push subscription (FR-NOT-001's "web push for installed PWAs"). Endpoint is
/// unique — resubscribing with the same endpoint upserts rather than duplicating. Deleted by the
/// dispatcher itself when a push attempt comes back 404/410 (the browser dropped the
/// subscription), never by any other failure.
/// </summary>
public sealed class PushSubscription
{
    private PushSubscription()
    {
    }

    private PushSubscription(Guid id, Guid userId, string endpoint, string p256Dh, string auth, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        Endpoint = endpoint;
        P256Dh = p256Dh;
        Auth = auth;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Endpoint { get; private set; } = string.Empty;

    public string P256Dh { get; private set; } = string.Empty;

    public string Auth { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static PushSubscription Create(Guid id, Guid userId, string endpoint, string p256Dh, string auth, DateTimeOffset now) =>
        new(id, userId, endpoint, p256Dh, auth, now);

    public void UpdateKeys(string p256Dh, string auth)
    {
        P256Dh = p256Dh;
        Auth = auth;
    }
}
