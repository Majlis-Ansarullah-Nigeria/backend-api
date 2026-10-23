using HRS.Application.Features.Audit;

namespace HRS.Application.UnitTests.Features.Audit;

public class AuditFormattingTests
{
    [Theory]
    [InlineData("102.89.12.34", "102.89.x.x")]
    [InlineData("::ffff:197.210.5.6", "197.210.x.x")]
    [InlineData("2001:db8:85a3::8a2e:370:7334", "2001:db8:x:x")]
    [InlineData("not-an-ip", "x.x.x.x")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void MasksAllButTheNetworkPart(string? ip, string? expected)
    {
        Assert.Equal(expected, IpMask.Mask(ip));
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line\nbreak", "\"line\nbreak\"")]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("-2", "'-2")]
    [InlineData(null, "")]
    public void EscapesCsvValuesAndDefusesFormulas(string? value, string expected)
    {
        Assert.Equal(expected, Csv.Escape(value));
    }
}
