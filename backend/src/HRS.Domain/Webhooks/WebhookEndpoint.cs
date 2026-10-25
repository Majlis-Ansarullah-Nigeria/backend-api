namespace HRS.Domain.Webhooks;

/// <summary>
/// FR-ADM-003: another system that wants report events. Only HTTPS URLs; every delivery is signed
/// with the endpoint's own secret (HMAC-SHA256) so the receiver can verify it came from here.
/// </summary>
public sealed class WebhookEndpoint
{
    public const int MaxUrlLength = 500;

    private List<WebhookEvent> _events = [];

    private WebhookEndpoint()
    {
    }

    public Guid Id { get; private set; }

    public string Url { get; private set; } = string.Empty;

    /// <summary>The signing secret. Shown once when created or rotated; never returned again.</summary>
    public string Secret { get; private set; } = string.Empty;

    public IReadOnlyList<WebhookEvent> Events => _events;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static WebhookEndpoint Create(Guid id, string url, IEnumerable<WebhookEvent> events, string secret, Guid userId, DateTimeOffset now)
    {
        var endpoint = new WebhookEndpoint { Id = id, Secret = secret, IsActive = true, CreatedAt = now, CreatedByUserId = userId };
        endpoint.Apply(url, events, true);
        return endpoint;
    }

    public void Update(string url, IEnumerable<WebhookEvent> events, bool isActive, DateTimeOffset now)
    {
        Apply(url, events, isActive);
        UpdatedAt = now;
    }

    public void RotateSecret(string secret, DateTimeOffset now)
    {
        Secret = secret;
        UpdatedAt = now;
    }

    public bool Wants(WebhookEvent webhookEvent) => IsActive && (webhookEvent == WebhookEvent.Ping || _events.Contains(webhookEvent));

    private void Apply(string url, IEnumerable<WebhookEvent> events, bool isActive)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || url.Length > MaxUrlLength)
        {
            throw new ArgumentException("A webhook URL must be an absolute https:// address.", nameof(url));
        }

        var chosen = events.Where(e => e != WebhookEvent.Ping).Distinct().ToList();
        if (chosen.Count == 0)
        {
            throw new ArgumentException("Choose at least one event.", nameof(events));
        }

        Url = url;
        _events = chosen;
        IsActive = isActive;
    }
}
