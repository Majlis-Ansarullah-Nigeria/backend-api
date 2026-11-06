using System.Text;
using ManagementApi.Domain.Enums;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// Reference numbers: {TYPECODE}-{PERIOD}-{ZONE}{DILA}{MUQAM}, e.g. GMR-2026-11-Z03D12M04.
/// The period comes from the window's opening date and the type's frequency:
/// monthly 2026-11, quarterly 2026-Q4, annual 2026. Each unit part is its code,
/// or a short stable code from its id when no code was entered.
/// </summary>
public static class ReportReferenceNumber
{
    public static string Period(ReportFrequency frequency, DateTime windowOpensAt) => frequency switch
    {
        ReportFrequency.Quarterly => $"{windowOpensAt:yyyy}-Q{(windowOpensAt.Month - 1) / 3 + 1}",
        ReportFrequency.Annual => $"{windowOpensAt:yyyy}",
        _ => $"{windowOpensAt:yyyy-MM}"
    };

    public static string UnitPart(char prefix, string? code, Guid id)
    {
        var cleaned = Clean(code);
        return string.IsNullOrEmpty(cleaned)
            ? prefix + id.ToString("N")[..4].ToUpperInvariant()
            : cleaned;
    }

    public static string Build(string typeCode, string period, params string[] unitParts) =>
        $"{typeCode}-{period}-{string.Concat(unitParts)}";

    /// <summary>Adds -2, -3 … when the same type, period and unit already have a reference in another window.</summary>
    public static string WithSuffix(string reference, int attempt) =>
        attempt <= 1 ? reference : $"{reference}-{attempt}";

    /// <summary>Fallback type code for templates not yet linked to a report type.</summary>
    public static string FallbackTypeCode(string? templateReportType)
    {
        var cleaned = Clean(templateReportType);
        return cleaned.Length >= 2 ? cleaned[..Math.Min(3, cleaned.Length)] : "RPT";
    }

    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var c in value.ToUpperInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
