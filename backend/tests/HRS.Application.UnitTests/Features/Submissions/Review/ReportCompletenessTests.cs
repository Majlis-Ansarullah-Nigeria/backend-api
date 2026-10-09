using HRS.Application.Features.Submissions.Review;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Application.UnitTests.Features.Submissions.Review;

public class ReportCompletenessTests
{
    private static TemplateField Field(
        string key, bool required = true, TemplateFieldType type = TemplateFieldType.ShortText, TemplateShowIf? showIf = null) =>
        new(key, type, key, null, required, null, null, null, null, null, false,
            null, null, showIf, AggregationRule.None, null, null, null);

    [Fact]
    public void AnUnansweredRequiredFieldIsMissing()
    {
        var schema = new TemplateSchema([new TemplateSection("s1", "Section 1", [Field("name")])]);

        var missing = ReportCompleteness.FindMissingRequiredFields(schema, UnitLevelCode.Muqam, new Dictionary<string, object?>());

        Assert.Single(missing);
        Assert.Equal("name", missing[0].Key);
    }

    [Fact]
    public void AnAnsweredRequiredFieldIsNotMissing()
    {
        var schema = new TemplateSchema([new TemplateSection("s1", "Section 1", [Field("name")])]);

        var missing = ReportCompleteness.FindMissingRequiredFields(
            schema, UnitLevelCode.Muqam, new Dictionary<string, object?> { ["name"] = "Agodi Muqam" });

        Assert.Empty(missing);
    }

    [Fact]
    public void AnOptionalUnansweredFieldIsNeverMissing()
    {
        var schema = new TemplateSchema([new TemplateSection("s1", "Section 1", [Field("notes", required: false)])]);

        var missing = ReportCompleteness.FindMissingRequiredFields(schema, UnitLevelCode.Muqam, new Dictionary<string, object?>());

        Assert.Empty(missing);
    }

    [Fact]
    public void AHiddenShowIfGatedRequiredFieldIsNeverMissing()
    {
        var schema = new TemplateSchema([
            new TemplateSection("s1", "Section 1", [Field("attendance", showIf: new TemplateShowIf("held_meeting", "Yes"))]),
        ]);

        var missing = ReportCompleteness.FindMissingRequiredFields(
            schema, UnitLevelCode.Muqam, new Dictionary<string, object?> { ["held_meeting"] = "No" });

        Assert.Empty(missing);
    }

    [Fact]
    public void AComputedFieldIsNeverMissingEvenIfMarkedRequired()
    {
        var schema = new TemplateSchema([new TemplateSection("s1", "Section 1", [Field("total", type: TemplateFieldType.Computed)])]);

        var missing = ReportCompleteness.FindMissingRequiredFields(schema, UnitLevelCode.Muqam, new Dictionary<string, object?>());

        Assert.Empty(missing);
    }
}
