using System.Collections.Concurrent;
using HRS.Application.Common.Interfaces;

namespace HRS.Api.IntegrationTests;

/// <summary>
/// Replaces the real <see cref="IWebhookSender"/> in tests — no network I/O. Keyed by URL so a test
/// can assert what its own endpoint received without counting across the shared test database.
/// </summary>
public sealed class CapturingWebhookSender : IWebhookSender
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<WebhookRequest>> _sentByUrl = new();

    /// <summary>URLs that answer 500, exercising the dispatcher's retry path.</summary>
    public ConcurrentDictionary<string, bool> FailingUrls { get; } = new();

    public Task<WebhookSendResult> SendAsync(WebhookRequest request, CancellationToken cancellationToken)
    {
        _sentByUrl.GetOrAdd(request.Url, _ => new ConcurrentQueue<WebhookRequest>()).Enqueue(request);
        return Task.FromResult(FailingUrls.ContainsKey(request.Url)
            ? new WebhookSendResult(false, 500, "Internal Server Error")
            : new WebhookSendResult(true, 200, null));
    }

    public IReadOnlyList<WebhookRequest> SentTo(string url) =>
        _sentByUrl.TryGetValue(url, out var sent) ? [.. sent] : [];
}
