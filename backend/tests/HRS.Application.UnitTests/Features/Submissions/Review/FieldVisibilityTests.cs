using HRS.Application.Features.Submissions.Review;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Application.UnitTests.Features.Submissions.Review;

public class FieldVisibilityTests
{
    private static TemplateField Field(
        string key = "field_a",
        TemplateFieldType type = TemplateFieldType.ShortText,
        bool required = false,
        IReadOnlyList<UnitLevelCode>? visibleForLevels = null,
        TemplateShowIf? showIf = null) =>
        new(key, type, "Field A", null, required, null, null, null, null, null, false,
            null, visibleForLevels, showIf, AggregationRule.None, null, null, null);

    [Fact]
    public void AFieldWithNoVisibleForLevelsIsVisibleAtEveryLevel()
    {
        var field = Field();

        Assert.True(FieldVisibility.IsVisibleForLevel(field, UnitLevelCode.Muqam));
        Assert.True(FieldVisibility.IsVisibleForLevel(field, UnitLevelCode.National));
    }

    [Fact]
    public void AFieldRestrictedToSomeLevelsIsHiddenAtOthers()
    {
        var field = Field(visibleForLevels: [UnitLevelCode.Muqam]);

        Assert.True(FieldVisibility.IsVisibleForLevel(field, UnitLevelCode.Muqam));
        Assert.False(FieldVisibility.IsVisibleForLevel(field, UnitLevelCode.Zone));
    }

    [Fact]
    public void AFieldWithNoShowIfIsAlwaysShown()
    {
        var field = Field();

        Assert.True(FieldVisibility.IsShown(field, new Dictionary<string, object?>()));
    }

    [Fact]
    public void AFieldWithAShowIfIsHiddenUntilTheTriggerMatches()
    {
        var field = Field(showIf: new TemplateShowIf("held_meeting", "Yes"));

        Assert.False(FieldVisibility.IsShown(field, new Dictionary<string, object?> { ["held_meeting"] = "No" }));
        Assert.True(FieldVisibility.IsShown(field, new Dictionary<string, object?> { ["held_meeting"] = "Yes" }));
    }

    [Fact]
    public void ShowIfComparisonIsCaseInsensitiveAndTrims()
    {
        var field = Field(showIf: new TemplateShowIf("held_meeting", " Yes "));

        Assert.True(FieldVisibility.IsShown(field, new Dictionary<string, object?> { ["held_meeting"] = "yes" }));
    }

    [Fact]
    public void ShowIfTreatsABooleanAnswerAsItsStringForm()
    {
        var field = Field(showIf: new TemplateShowIf("held_meeting", "true"));

        Assert.True(FieldVisibility.IsShown(field, new Dictionary<string, object?> { ["held_meeting"] = true }));
    }

    [Fact]
    public void VisibleFieldsCombinesBothChecks()
    {
        var fields = new[]
        {
            Field(key: "always"),
            Field(key: "muqam_only", visibleForLevels: [UnitLevelCode.Muqam]),
            Field(key: "conditional", showIf: new TemplateShowIf("always", "shown")),
        };

        var result = FieldVisibility.VisibleFields(fields, UnitLevelCode.Zone, new Dictionary<string, object?> { ["always"] = "shown" }).ToList();

        Assert.Contains(result, f => f.Key == "always");
        Assert.DoesNotContain(result, f => f.Key == "muqam_only");
        Assert.Contains(result, f => f.Key == "conditional");
    }
}
