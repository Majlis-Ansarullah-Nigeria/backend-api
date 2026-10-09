using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Application.Features.Submissions.Review;

/// <summary>Submit's hard gate (FR-SUB-006) — the review screen computes the same thing
/// client-side to disable its button, but this is what actually stops a bypassed/buggy client
/// from submitting an incomplete report.</summary>
public static class ReportCompleteness
{
    public static IReadOnlyList<TemplateField> FindMissingRequiredFields(
        TemplateSchema schema, UnitLevelCode unitLevel, IReadOnlyDictionary<string, object?> answers) =>
        schema.Sections
            .SelectMany(s => FieldVisibility.VisibleFields(s.Fields, unitLevel, answers))
            .Where(f => f.Required && f.Type != TemplateFieldType.Computed)
            .Where(f => !AnswerValue.HasValue(answers.GetValueOrDefault(f.Key)))
            .ToList();
}
