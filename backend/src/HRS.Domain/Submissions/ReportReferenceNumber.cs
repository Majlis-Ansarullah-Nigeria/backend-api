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
    /// <param name="typeCode">The report type's own code (ReportType.Code), never derived from the
    /// category: two types of one category would otherwise issue the same reference.</param>
    public static string Generate(string typeCode, ReportTypeCategory category, string periodLabel, DateTimeOffset fallbackDate, string unitCode)
    {
        var period = ResolvePeriod(category, periodLabel, fallbackDate);
        var unitSegment = unitCode.Replace("-", "", StringComparison.Ordinal);

        return $"{typeCode}-{period}-{unitSegment}";
    }

    /// <summary>A reference that's already taken (the same type, period and unit in a second
    /// window) gets the next free suffix: <c>…-Z03D12M04-2</c>, <c>-3</c>, … — never a failure.</summary>
    public static string WithSuffix(string reference, int attempt) =>
        attempt <= 1 ? reference : string.Create(CultureInfo.InvariantCulture, $"{reference}-{attempt}");

    /// <summary>FR-REV-006's compiled national report: <c>NAT-{TYPECODE}-{period}</c>, e.g.
    /// <c>NAT-GMR-2026-09</c> (the "Compiled national report" mockup).</summary>
    public static string GenerateNational(string typeCode, ReportTypeCategory category, string periodLabel, DateTimeOffset fallbackDate) =>
        $"NAT-{typeCode}-{ResolvePeriod(category, periodLabel, fallbackDate)}";

    /// <summary>The category's conventional code — the starting suggestion for a new report
    /// type's own code (ReportType.Code).</summary>
    public static string CategoryCode(ReportTypeCategory category) =>
        category switch
        {
            ReportTypeCategory.GeneralMonthly => "GMR",
            ReportTypeCategory.Quarterly => "QTR",
            ReportTypeCategory.Annual => "ANN",
            ReportTypeCategory.AdHoc => "ADH",
            ReportTypeCategory.Event => "EVT",
            ReportTypeCategory.Survey => "SUR",
            _ => "RPT",
        };

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
