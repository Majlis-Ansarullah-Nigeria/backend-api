using System.Net;
using System.Text.Json;
using HRS.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using WebPush;
using DomainPushSubscription = HRS.Domain.Notifications.PushSubscription;

namespace HRS.Infrastructure.Notifications;

public sealed class WebPushNotificationSender(IOptions<PushOptions> options) : IPushNotificationSender
{
    public async Task SendAsync(DomainPushSubscription subscription, PushPayload payload, CancellationToken cancellationToken)
    {
        var pushOptions = options.Value;
        var client = new WebPushClient();
        var vapidDetails = new VapidDetails(pushOptions.VapidSubject, pushOptions.VapidPublicKey, pushOptions.VapidPrivateKey);
        var pushSubscription = new PushSubscription(subscription.Endpoint, subscription.P256Dh, subscription.Auth);
        var json = JsonSerializer.Serialize(new { title = payload.Title, body = payload.Body, link = payload.Link });

        try
        {
            await client.SendNotificationAsync(pushSubscription, json, vapidDetails, cancellationToken);
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            throw new PushSubscriptionGoneException();
        }
    }
}
