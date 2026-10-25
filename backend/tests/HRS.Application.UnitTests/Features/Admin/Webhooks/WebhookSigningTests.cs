using System.Net;
using HRS.Application.Features.Admin.Webhooks;

namespace HRS.Application.UnitTests.Features.Admin.Webhooks;

public class WebhookSigningTests
{
    [Fact]
    public void TheSignatureIsHmacSha256OverTimestampDotBody()
    {
        // Reference value: printf '1700000000.{"a":1}' | openssl dgst -sha256 -hmac whsec_test
        var signature = WebhookSigning.Sign("whsec_test", 1_700_000_000, "{\"a\":1}");

        Assert.Equal("t=1700000000,v1=38877139021993b830af32feea6e18a8da83eb2f6e49ee50bd9e4cf4ca4d3789", signature);
        Assert.NotEqual(signature, WebhookSigning.Sign("whsec_other", 1_700_000_000, "{\"a\":1}"));
        Assert.NotEqual(signature, WebhookSigning.Sign("whsec_test", 1_700_000_001, "{\"a\":1}"));
    }

    [Fact]
    public void SecretsAreRandomAndOnlyTheirLastFourCharactersAreHinted()
    {
        var secret = WebhookSigning.NewSecret();

        Assert.StartsWith("whsec_", secret, StringComparison.Ordinal);
        Assert.NotEqual(secret, WebhookSigning.NewSecret());
        Assert.Equal("…" + secret[^4..], WebhookSigning.Hint(secret));
    }
}

public class WebhookAddressPolicyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:10.0.0.1")]
    public void PrivateLoopbackAndMetadataAddressesAreRefused(string address) =>
        Assert.False(WebhookAddressPolicy.IsPublic(IPAddress.Parse(address)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("2606:4700:4700::1111")]
    public void PublicAddressesAreAllowed(string address) =>
        Assert.True(WebhookAddressPolicy.IsPublic(IPAddress.Parse(address)));
}
