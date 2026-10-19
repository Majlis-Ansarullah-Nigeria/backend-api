using System.Text.Json;
using HRS.Application.Features.Submissions.Review;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Application.Features.History;

public sealed record ReportFieldChange(string SectionTitle, string FieldKey, string Label, string Before, string After);

/// <summary>Field-by-field difference between two answer snapshots of the same template version
/// (FR-HIS-006). Computed fields are derived and file uploads aren't stored in the answers, so
/// both are skipped, same as the receipt (ReportReceiptBuilder). Confidential fields are left
/// out entirely for observers — not even the fact that one changed is shown (FR-AC-006).</summary>
public static class AnswerDiff
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoAttachments =
        new Dictionary<string, IReadOnlyList<string>>();

    public static IReadOnlyList<ReportFieldChange> Compare(
        TemplateSchema schema, UnitLevelCode unitLevel, string beforeJson, string afterJson, bool hideConfidential)
    {
        var before = Parse(beforeJson);
        var after = Parse(afterJson);
        var changes = new List<ReportFieldChange>();

        foreach (var section in schema.Sections)
        {
            foreach (var field in section.Fields)
            {
                if (field.Type is TemplateFieldType.Computed or TemplateFieldType.FileUpload
                    || !FieldVisibility.IsVisibleForLevel(field, unitLevel)
                    || (hideConfidential && field.Confidential))
                {
                    continue;
                }

                var beforeValue = before.GetValueOrDefault(field.Key);
                var afterValue = after.GetValueOrDefault(field.Key);
                if (AreEqual(beforeValue, afterValue))
                {
                    continue;
                }

                changes.Add(new ReportFieldChange(
                    section.Title,
                    field.Key,
                    field.Label,
                    AnswerDisplay.Format(field, beforeValue, NoAttachments),
                    AnswerDisplay.Format(field, afterValue, NoAttachments)));
            }
        }

        return changes;
    }

    public static Dictionary<string, object?> Parse(string answersJson) =>
        JsonSerializer.Deserialize<Dictionary<string, object?>>(answersJson, JsonSerializerOptions.Web) ?? [];

    private static bool AreEqual(object? before, object? after)
    {
        var beforeHasValue = AnswerValue.HasValue(before);
        var afterHasValue = AnswerValue.HasValue(after);
        if (!beforeHasValue || !afterHasValue)
        {
            return beforeHasValue == afterHasValue;
        }

        var beforeNumber = AnswerValue.ToDoubleOrNull(before);
        var afterNumber = AnswerValue.ToDoubleOrNull(after);
        if (beforeNumber is not null && afterNumber is not null)
        {
            return beforeNumber.Value.Equals(afterNumber.Value);
        }

        return JsonSerializer.Serialize(before) == JsonSerializer.Serialize(after);
    }
}
