using HRS.Domain.Webhooks;

namespace HRS.Domain.UnitTests.Webhooks;

public class WebhookEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("http://hooks.example.org/x")]
    [InlineData("hooks.example.org/x")]
    [InlineData("ftp://hooks.example.org/x")]
    public void OnlyAbsoluteHttpsUrlsAreAccepted(string url) =>
        Assert.Throws<ArgumentException>(() =>
            WebhookEndpoint.Create(Guid.NewGuid(), url, [WebhookEvent.ReportSubmitted], "s", Guid.NewGuid(), Now));

    [Fact]
    public void PingIsNotASubscribableEventButEveryActiveEndpointWantsIt()
    {
        Assert.Throws<ArgumentException>(() =>
            WebhookEndpoint.Create(Guid.NewGuid(), "https://h.example.org", [WebhookEvent.Ping], "s", Guid.NewGuid(), Now));

        var endpoint = WebhookEndpoint.Create(
            Guid.NewGuid(), "https://h.example.org", [WebhookEvent.ReportSubmitted, WebhookEvent.Ping, WebhookEvent.ReportSubmitted], "s", Guid.NewGuid(), Now);

        Assert.Equal([WebhookEvent.ReportSubmitted], endpoint.Events);
        Assert.True(endpoint.Wants(WebhookEvent.Ping));
        Assert.False(endpoint.Wants(WebhookEvent.ReportAcknowledged));

        endpoint.Update("https://h.example.org", [WebhookEvent.ReportSubmitted], isActive: false, Now);
        Assert.False(endpoint.Wants(WebhookEvent.ReportSubmitted));
        Assert.False(endpoint.Wants(WebhookEvent.Ping));
    }
}

public class WebhookDeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FailuresBackOffAndTheSixthFailureGivesUp()
    {
        var delivery = WebhookDelivery.Create(Guid.NewGuid(), Guid.NewGuid(), WebhookEvent.Ping, "{}", Now);

        foreach (var wait in WebhookDelivery.Backoff)
        {
            delivery.RecordFailure(503, "Service Unavailable", Now);
            Assert.Equal((WebhookDeliveryStatus.Pending, Now + wait), (delivery.Status, delivery.NextAttemptAt));
        }

        delivery.RecordFailure(503, "Service Unavailable", Now);
        Assert.Equal((WebhookDeliveryStatus.Failed, 6), (delivery.Status, delivery.Attempts));
    }

    [Fact]
    public void SuccessClearsTheLastErrorAndLongErrorsAreTrimmed()
    {
        var delivery = WebhookDelivery.Create(Guid.NewGuid(), Guid.NewGuid(), WebhookEvent.Ping, "{}", Now);
        delivery.RecordFailure(null, new string('x', 900), Now);
        Assert.Equal(500, delivery.LastError!.Length);

        delivery.RecordSuccess(204, Now.AddMinutes(1));
        Assert.Equal((WebhookDeliveryStatus.Delivered, 2, 204, null, Now.AddMinutes(1)),
            (delivery.Status, delivery.Attempts, delivery.LastStatusCode, delivery.LastError, delivery.DeliveredAt));
    }
}
