using System.Globalization;
using System.Text.Json;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Templates;

namespace HRS.Application.Features.Submissions.Review;

/// <summary>Turns one raw answer value into human-readable text for the receipt PDF (FR-SUB-010)
/// — not pixel-perfect for every field type, just legible. FileUpload fields don't store their
/// value in AnswersJson at all (M5.4) — their filenames come from attachmentsByField, looked up
/// by the caller from ReportAttachment rows.</summary>
public static class AnswerDisplay
{
    public static string Format(TemplateField field, object? value, IReadOnlyDictionary<string, IReadOnlyList<string>> attachmentsByField)
    {
        if (field.Type == TemplateFieldType.FileUpload)
        {
            var files = attachmentsByField.GetValueOrDefault(field.Key, []);
            return files.Count == 0 ? "No files attached" : string.Join(", ", files);
        }

        if (!AnswerValue.HasValue(value))
        {
            return "—";
        }

        return field.Type switch
        {
            TemplateFieldType.YesNo => AnswerValue.ToComparableString(value) == "true" ? "Yes" : "No",
            TemplateFieldType.SingleChoice or TemplateFieldType.Dropdown => OptionLabel(field, AnswerValue.ToComparableString(value)),
            TemplateFieldType.MultipleChoice => string.Join(", ", ToStringList(value).Select(v => OptionLabel(field, v))),
            TemplateFieldType.MemberList => string.Join(", ", ToStringList(value)),
            TemplateFieldType.DateRange => FormatPair(value, "start", "end", " – "),
            TemplateFieldType.Location => FormatPair(value, "latitude", "longitude", ", "),
            TemplateFieldType.RepeatableTable => FormatRowCount(value),
            TemplateFieldType.Currency or TemplateFieldType.Number => FormatNumber(value, field.UnitLabel),
            TemplateFieldType.Percentage => FormatPercentage(value),
            _ => AnswerValue.ToComparableString(value),
        };
    }

    private static string OptionLabel(TemplateField field, string value) =>
        field.Options?.FirstOrDefault(o => o.Value == value)?.Label ?? value;

    private static List<string> ToStringList(object? value)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Array } element)
        {
            return [.. element.EnumerateArray().Select(e => AnswerValue.ToComparableString(e))];
        }

        return value is System.Collections.IEnumerable enumerable and not string
            ? [.. enumerable.Cast<object?>().Select(AnswerValue.ToComparableString)]
            : [];
    }

    private static string FormatPair(object? value, string key1, string key2, string separator)
    {
        if (value is not JsonElement { ValueKind: JsonValueKind.Object } element)
        {
            return "—";
        }

        var v1 = element.TryGetProperty(key1, out var p1) ? AnswerValue.ToComparableString(p1) : "";
        var v2 = element.TryGetProperty(key2, out var p2) ? AnswerValue.ToComparableString(p2) : "";
        return $"{v1}{separator}{v2}";
    }

    private static string FormatRowCount(object? value)
    {
        var count = value is JsonElement { ValueKind: JsonValueKind.Array } element ? element.GetArrayLength() : 0;
        return count == 1 ? "1 row" : $"{count.ToString(CultureInfo.InvariantCulture)} rows";
    }

    private static string FormatNumber(object? value, string? unitLabel)
    {
        var number = AnswerValue.ToDoubleOrNull(value);
        if (number is null)
        {
            return AnswerValue.ToComparableString(value);
        }

        var formatted = number.Value.ToString("#,0.##", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(unitLabel) ? formatted : $"{formatted} {unitLabel}";
    }

    private static string FormatPercentage(object? value)
    {
        var number = AnswerValue.ToDoubleOrNull(value);
        return number is null ? AnswerValue.ToComparableString(value) : $"{number.Value.ToString("0.##", CultureInfo.InvariantCulture)}%";
    }
}
