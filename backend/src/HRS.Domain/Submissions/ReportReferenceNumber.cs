using System.Globalization;
using System.Text.RegularExpressions;
using HRS.Domain.Templates;

namespace HRS.Domain.Submissions;

/// <summary>
/// FR-SUB-010's format: <c>{TYPECODE}-{period}-{UNITCODE without dashes}</c>, e.g.
/// <c>GMR-2026-09-Z03D12M04</c> or, for a quarterly report, <c>QTR-2026-Q3-...</c> (hrs-domain
/// skill). The period segment is parsed from SubmissionWindow.PeriodLabel — the only place a
/// human-readable reporting period is actually stored (OpensAt/DueAt are offset from it by
/// whatever a schedule's day-offsets say, not reliably "the period itself") — with a same-shape
/// fallback to a supplied date if parsing fails, since a reference number must never fail to
/// generate.
/// </summary>
public static partial class ReportReferenceNumber
{
    public static string Generate(ReportTypeCategory category, string periodLabel, DateTimeOffset fallbackDate, string unitCode)
    {
        var typeCode = category switch
        {
            ReportTypeCategory.GeneralMonthly => "GMR",
            ReportTypeCategory.Quarterly => "QTR",
            ReportTypeCategory.Annual => "ANN",
            ReportTypeCategory.AdHoc => "ADH",
            ReportTypeCategory.Event => "EVT",
            ReportTypeCategory.Survey => "SUR",
            _ => "RPT",
        };

        var period = ResolvePeriod(category, periodLabel, fallbackDate);
        var unitSegment = unitCode.Replace("-", "", StringComparison.Ordinal);

        return $"{typeCode}-{period}-{unitSegment}";
    }

    private static string ResolvePeriod(ReportTypeCategory category, string periodLabel, DateTimeOffset fallbackDate) =>
        category switch
        {
            ReportTypeCategory.Quarterly => ResolveQuarter(periodLabel) ?? FallbackQuarter(fallbackDate),
            ReportTypeCategory.Annual => ResolveYear(periodLabel) ?? fallbackDate.Year.ToString(CultureInfo.InvariantCulture),
            ReportTypeCategory.AdHoc or ReportTypeCategory.Event or ReportTypeCategory.Survey =>
                fallbackDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => ResolveMonth(periodLabel) ?? fallbackDate.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        };

    private static string? ResolveMonth(string periodLabel) =>
        DateTime.TryParseExact(periodLabel.Trim(), "MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            : null;

    private static string? ResolveQuarter(string periodLabel)
    {
        var match = QuarterPattern().Match(periodLabel);
        return match.Success ? $"{match.Groups["year"].Value}-Q{match.Groups["quarter"].Value}" : null;
    }

    private static string FallbackQuarter(DateTimeOffset fallbackDate) =>
        $"{fallbackDate.Year.ToString(CultureInfo.InvariantCulture)}-Q{(fallbackDate.Month - 1) / 3 + 1}";

    private static string? ResolveYear(string periodLabel)
    {
        var match = YearPattern().Match(periodLabel);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"Q(?<quarter>[1-4])\D*(?<year>\d{4})|(?<year>\d{4})\D*Q(?<quarter>[1-4])", RegexOptions.IgnoreCase)]
    private static partial Regex QuarterPattern();

    [GeneratedRegex(@"\d{4}")]
    private static partial Regex YearPattern();
}
