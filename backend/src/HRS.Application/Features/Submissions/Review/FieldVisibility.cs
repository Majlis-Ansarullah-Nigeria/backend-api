using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Organization;

namespace HRS.Application.Features.Submissions.Review;

/// <summary>
/// A C# port of the frontend's field-visibility.ts, used server-side only to enforce Submit's
/// hard completeness gate (FR-SUB-006) — the UI already runs the equivalent logic to disable the
/// Submit button, but the API must not trust that alone (backend/CLAUDE.md: "The UI only hides
/// what the API already forbids"). Conditional-logic evaluation itself stays in sync by being
/// this small and literal; architecture.md's "two implementations, tested against shared
/// fixtures" requirement is for the richer computed-expression language (M3.3), not this
/// single-field equality check.
/// </summary>
public static class FieldVisibility
{
    public static bool IsVisibleForLevel(TemplateField field, UnitLevelCode unitLevel) =>
        field.VisibleForLevels is not { Count: > 0 } levels || levels.Contains(unitLevel);

    public static bool IsShown(TemplateField field, IReadOnlyDictionary<string, object?> answers)
    {
        if (field.ShowIf is not { } showIf)
        {
            return true;
        }

        var actual = AnswerValue.ToComparableString(answers.GetValueOrDefault(showIf.FieldKey));
        return string.Equals(actual, showIf.EqualsValue.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<TemplateField> VisibleFields(
        IEnumerable<TemplateField> fields, UnitLevelCode unitLevel, IReadOnlyDictionary<string, object?> answers) =>
        fields.Where(f => IsVisibleForLevel(f, unitLevel) && IsShown(f, answers));
}
