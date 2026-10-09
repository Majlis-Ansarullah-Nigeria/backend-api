using System.Globalization;
using System.Text.Json;

namespace HRS.Application.Features.Submissions.Review;

/// <summary>
/// Deserializing ReportInstance.AnswersJson into Dictionary&lt;string, object?&gt; leaves each
/// value as a JsonElement — System.Text.Json's default representation for an untyped `object`
/// target. These helpers unwrap that uniformly (mirroring the frontend's
/// section-progress.ts/field-visibility.ts logic) and also accept plain CLR values directly, so
/// tests can build answer dictionaries by hand without round-tripping through JSON.
/// </summary>
public static class AnswerValue
{
    /// <summary>FR-SUB-004's "has this field been answered" — null/empty string/empty
    /// array/all-nested-empty counts as unanswered.</summary>
    public static bool HasValue(object? value) =>
        value switch
        {
            null => false,
            JsonElement element => element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => false,
                JsonValueKind.String => element.GetString()?.Length > 0,
                JsonValueKind.Array => element.EnumerateArray().Any(),
                JsonValueKind.Object => element.EnumerateObject().Any(p => HasValue(p.Value)),
                _ => true,
            },
            string s => s.Length > 0,
            IDictionary<string, object?> dict => dict.Values.Any(HasValue),
            System.Collections.IEnumerable enumerable => enumerable.Cast<object?>().Any(),
            _ => true,
        };

    /// <summary>showIf's equality comparison — the answer is coerced to the same canonical
    /// string form a yes/no, number or text field would naturally produce.</summary>
    public static string ToComparableString(object? value) =>
        value switch
        {
            null => "",
            JsonElement element => element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => "",
                JsonValueKind.String => element.GetString()?.Trim() ?? "",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => element.GetRawText(),
                _ => element.GetRawText(),
            },
            bool b => b ? "true" : "false",
            _ => value.ToString()?.Trim() ?? "",
        };

    public static double? ToDoubleOrNull(object? value) =>
        value switch
        {
            JsonElement { ValueKind: JsonValueKind.Number } element => element.GetDouble(),
            JsonElement { ValueKind: JsonValueKind.String } element =>
                double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFromJson) ? parsedFromJson : null,
            null => null,
            double d => d,
            int i => i,
            long l => l,
            decimal m => (double)m,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
}
