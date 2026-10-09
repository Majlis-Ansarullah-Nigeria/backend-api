namespace HRS.Application.Common.Interfaces;

/// <summary>Scans a file's bytes before it's accepted (FR-SUB-005: "virus-scanned before
/// acceptance").</summary>
public interface IVirusScanner
{
    Task<VirusScanResult> ScanAsync(Stream content, CancellationToken cancellationToken);
}

public enum VirusScanResult
{
    Clean,
    Infected,
}
