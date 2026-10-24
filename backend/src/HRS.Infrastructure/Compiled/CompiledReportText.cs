using System.Globalization;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Review.Compiled;
using HRS.Domain.Templates;

namespace HRS.Infrastructure.Compiled;

/// <summary>The English wording both exports share, so the PDF and the Word file say exactly the
/// same thing. The web view formats the same data through the frontend's i18n layer.</summary>
internal static class CompiledReportText
{
    public static string Figure(double? value, TemplateFieldType type, string? unitLabel)
    {
        if (value is not { } number)
        {
            return "—";
        }

        var formatted = number.ToString("#,0.##", CultureInfo.InvariantCulture);
        return type switch
        {
            TemplateFieldType.Percentage => $"{formatted}%",
            TemplateFieldType.Currency when !string.IsNullOrEmpty(unitLabel) => $"{unitLabel} {formatted}",
            _ => formatted,
        };
    }

    public static string Change(double? value, double? previous, TemplateFieldType type)
    {
        if (value is not { } current || previous is not { } before)
        {
            return "—";
        }

        var difference = current - before;
        var sign = difference > 0 ? "+" : string.Empty;
        return type == TemplateFieldType.Percentage
            ? $"{sign}{difference.ToString("0.#", CultureInfo.InvariantCulture)} points"
            : $"{sign}{difference.ToString("#,0.##", CultureInfo.InvariantCulture)}";
    }

    public static string OverviewSentence(CompiledReportView report, OrganizationBranding branding) =>
        $"Of {report.Overview.Expected} units expected to report, {report.Overview.OnTime} submitted on time, " +
        $"{report.Overview.Late} late and {report.Overview.NotReported} did not report. " +
        $"Figures include only acknowledged reports ({report.Overview.Acknowledged} so far), compiled on " +
        $"{branding.DateTime(report.CompiledAt)}.";

    public static string Highlight(CompiledHighlight highlight, CompiledFigure? figure)
    {
        var type = figure?.Type ?? TemplateFieldType.Number;
        var value = Figure(highlight.Value, type, figure?.UnitLabel);
        return highlight.Kind switch
        {
            CompiledHighlightKind.LargestRise =>
                $"{highlight.FieldLabel}: the largest rise was {highlight.ZoneName} ({Change(highlight.Value, highlight.Value - highlight.Change, type)}, now {value}).",
            CompiledHighlightKind.LargestFall =>
                $"{highlight.FieldLabel}: the largest fall was {highlight.ZoneName} ({Change(highlight.Value, highlight.Value - highlight.Change, type)}, now {value}).",
            _ => $"{highlight.FieldLabel}: highest in {highlight.ZoneName} ({value}).",
        };
    }
}
