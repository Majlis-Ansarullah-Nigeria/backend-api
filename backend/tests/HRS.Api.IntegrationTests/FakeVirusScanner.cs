using System.Text;
using HRS.Application.Common.Interfaces;

namespace HRS.Api.IntegrationTests;

/// <summary>
/// Replaces the real ClamAV-backed <see cref="IVirusScanner"/> in tests — spinning up a real
/// clamd (virus database download included) per test run would be slow and flaky. This fake
/// behaves exactly like a real scanner would against the EICAR test string (the standard,
/// harmless string every antivirus — including real ClamAV — flags as infected), so a test
/// exercising the "infected" path by uploading EICAR bytes would behave identically against the
/// real scanner too.
/// </summary>
public sealed class FakeVirusScanner : IVirusScanner
{
    public const string EicarTestString = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";

    public async Task<VirusScanResult> ScanAsync(Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, Encoding.ASCII, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        return text.Contains(EicarTestString, StringComparison.Ordinal) ? VirusScanResult.Infected : VirusScanResult.Clean;
    }
}
