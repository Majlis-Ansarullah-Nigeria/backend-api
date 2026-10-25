using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Admin.Webhooks;

namespace HRS.Infrastructure.Webhooks;

/// <summary>FR-ADM-003: POSTs a signed delivery and reports how it went. No redirects are followed,
/// and connections only go to public addresses (see <see cref="CreateHandler"/>).</summary>
public sealed class HttpWebhookSender(IHttpClientFactory httpClientFactory) : IWebhookSender
{
    public const string ClientName = "webhooks";

    public async Task<WebhookSendResult> SendAsync(WebhookRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, request.Url)
        {
            Content = new StringContent(request.Body, Encoding.UTF8, "application/json"),
        };
        message.Headers.UserAgent.Add(new ProductInfoHeaderValue("HRS-Webhooks", "1.0"));
        message.Headers.Add("X-HRS-Event", request.EventName);
        message.Headers.Add("X-HRS-Delivery", request.DeliveryId.ToString());
        message.Headers.Add("X-HRS-Signature", request.Signature);

        try
        {
            using var response = await httpClientFactory.CreateClient(ClientName).SendAsync(message, cancellationToken);
            var code = (int)response.StatusCode;
            return response.IsSuccessStatusCode
                ? new WebhookSendResult(true, code, null)
                : new WebhookSendResult(false, code, $"The receiver answered {code}.");
        }
        catch (HttpRequestException ex)
        {
            return new WebhookSendResult(false, null, ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new WebhookSendResult(false, null, "The receiver didn't answer within 10 seconds.");
        }
    }

    /// <summary>Resolves the host itself and connects only to a public address — checked on the
    /// address actually used, so DNS rebinding can't slip a private one in after validation.</summary>
    public static SocketsHttpHandler CreateHandler(bool allowPrivateNetworks) =>
        new()
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (context, cancellationToken) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
                var address = addresses.FirstOrDefault(a => allowPrivateNetworks || WebhookAddressPolicy.IsPublic(a))
                    ?? throw new HttpRequestException("The webhook address resolves to a private or reserved network.");
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
}
