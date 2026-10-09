namespace HRS.Domain.Common;

/// <summary>
/// Caches a state-changing request's successful result against the client-supplied
/// Idempotency-Key, scoped per user, so a retry on a poor connection replays the cached response
/// instead of reprocessing (SRS: "Submit and other state-changing calls accept an
/// Idempotency-Key header so retries on poor networks never double-submit"). Only successful
/// results are cached — a failed request made no state change, so simply retrying it is already
/// safe and needs no special handling.
/// </summary>
public sealed class IdempotencyRecord
{
    private IdempotencyRecord()
    {
    }

    private IdempotencyRecord(Guid id, Guid userId, string key, string responseJson, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        Key = key;
        ResponseJson = responseJson;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string ResponseJson { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static IdempotencyRecord Create(Guid id, Guid userId, string key, string responseJson, DateTimeOffset now) =>
        new(id, userId, key, responseJson, now);
}
