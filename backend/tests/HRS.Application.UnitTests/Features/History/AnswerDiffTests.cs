using HRS.Application.Features.History;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Application.UnitTests.Features.History;

public class AnswerDiffTests
{
    private static TemplateField Field(
        string key,
        TemplateFieldType type,
        bool confidential = false,
        IReadOnlyList<UnitLevelCode>? visibleForLevels = null) =>
        new(key, type, $"Label {key}", null, false, null, null, null, null, null, confidential,
            null, visibleForLevels, null, AggregationRule.None, null, null, null);

    private static TemplateSchema Schema(params TemplateField[] fields) =>
        new([new TemplateSection("s1", "Membership", fields)]);

    [Fact]
    public void ReportsOnlyTheFieldsThatChangedWithFormattedValues()
    {
        var schema = Schema(Field("members", TemplateFieldType.Number), Field("notes", TemplateFieldType.ShortText));

        var changes = AnswerDiff.Compare(
            schema, UnitLevelCode.Muqam, """{"members":142,"notes":"same"}""", """{"members":1480,"notes":"same"}""", hideConfidential: false);

        var change = Assert.Single(changes);
        Assert.Equal(("Membership", "members", "Label members", "142", "1,480"), (change.SectionTitle, change.FieldKey, change.Label, change.Before, change.After));
    }

    [Fact]
    public void TreatsNumericallyEqualValuesAndEmptyValuesAsUnchanged()
    {
        var schema = Schema(Field("members", TemplateFieldType.Number), Field("notes", TemplateFieldType.ShortText));

        var changes = AnswerDiff.Compare(
            schema, UnitLevelCode.Muqam, """{"members":"5","notes":""}""", """{"members":5.0}""", hideConfidential: false);

        Assert.Empty(changes);
    }

    [Fact]
    public void ShowsAnAddedAnswerAsChangedFromEmpty()
    {
        var schema = Schema(Field("notes", TemplateFieldType.ShortText));

        var change = Assert.Single(AnswerDiff.Compare(schema, UnitLevelCode.Muqam, "{}", """{"notes":"New"}""", hideConfidential: false));

        Assert.Equal(("—", "New"), (change.Before, change.After));
    }

    [Fact]
    public void LeavesOutConfidentialFieldsWhenHidingThem()
    {
        var schema = Schema(Field("welfare", TemplateFieldType.ShortText, confidential: true));

        Assert.Empty(AnswerDiff.Compare(schema, UnitLevelCode.Muqam, """{"welfare":"a"}""", """{"welfare":"b"}""", hideConfidential: true));
        Assert.Single(AnswerDiff.Compare(schema, UnitLevelCode.Muqam, """{"welfare":"a"}""", """{"welfare":"b"}""", hideConfidential: false));
    }

    [Fact]
    public void SkipsComputedFieldsAndFieldsNotVisibleAtTheUnitsLevel()
    {
        var schema = Schema(
            Field("total", TemplateFieldType.Computed),
            Field("zone_only", TemplateFieldType.Number, visibleForLevels: [UnitLevelCode.Zone]));

        Assert.Empty(AnswerDiff.Compare(
            schema, UnitLevelCode.Muqam, """{"total":1,"zone_only":1}""", """{"total":2,"zone_only":2}""", hideConfidential: false));
    }
}
