using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Templates;

namespace HRS.Application.UnitTests.Features.Templates.TemplateVersions;

public class TemplateSchemaValidatorTests
{
    private readonly TemplateSchemaValidator _validator = new();

    private static TemplateField NumberField(string key = "total_members", AggregationRule aggregation = AggregationRule.None) => new(
        key, TemplateFieldType.Number, "Total members", null, true, 0, null, null, null, "members", false,
        null, null, null, aggregation, null, null, null);

    private static TemplateSection Section(params TemplateField[] fields) =>
        new("membership", "Membership", fields);

    [Fact]
    public void AnEmptySchemaIsValid()
    {
        var result = _validator.Validate(new TemplateSchema([]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void AWellFormedNumberFieldIsValid()
    {
        var result = _validator.Validate(new TemplateSchema([Section(NumberField())]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void DuplicateFieldKeysAcrossSectionsFailValidation()
    {
        var schema = new TemplateSchema([
            new TemplateSection("s1", "Section 1", [NumberField("members")]),
            new TemplateSection("s2", "Section 2", [NumberField("members")]),
        ]);

        var result = _validator.Validate(schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("unique across the whole schema"));
    }

    [Fact]
    public void DuplicateSectionKeysFailValidation()
    {
        var schema = new TemplateSchema([
            new TemplateSection("s1", "Section 1", [NumberField("a")]),
            new TemplateSection("s1", "Section 1 Again", [NumberField("b")]),
        ]);

        var result = _validator.Validate(schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Section keys must be unique"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Total")]
    [InlineData("1total")]
    [InlineData("total members")]
    public void AnInvalidFieldKeyFailsValidation(string key)
    {
        var result = _validator.Validate(new TemplateSchema([Section(NumberField(key))]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AggregationOnANonNumericFieldFailsValidation()
    {
        var field = new TemplateField(
            "held_meeting", TemplateFieldType.YesNo, "Held meeting?", null, true, null, null, null, null, null,
            false, null, null, null, AggregationRule.Sum, null, null, null);

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Aggregation only applies"));
    }

    [Fact]
    public void AggregationOnANumericFieldIsValid()
    {
        var result = _validator.Validate(new TemplateSchema([Section(NumberField("members", AggregationRule.Sum))]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void MinGreaterThanMaxFailsValidation()
    {
        var field = NumberField() with { Min = 10, Max = 5 };

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AnInvalidRegexFailsValidation()
    {
        var field = NumberField() with { Regex = "[" };

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AChoiceFieldWithNoOptionsFailsValidation()
    {
        var field = new TemplateField(
            "fav_color", TemplateFieldType.Dropdown, "Favorite color", null, false, null, null, null, null, null,
            false, null, null, null, AggregationRule.None, null, null, null);

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("at least one option"));
    }

    [Fact]
    public void ANonChoiceFieldWithOptionsFailsValidation()
    {
        var field = NumberField() with { Options = [new TemplateFieldOption("a", "A")] };

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Only choice fields"));
    }

    [Fact]
    public void ADropdownWithOptionsIsValid()
    {
        var field = new TemplateField(
            "fav_color", TemplateFieldType.Dropdown, "Favorite color", null, false, null, null, null, null, null,
            false, null, null, null, AggregationRule.None, null,
            [new TemplateFieldOption("red", "Red"), new TemplateFieldOption("blue", "Blue")], null);

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void DuplicateOptionValuesFailValidation()
    {
        var field = new TemplateField(
            "fav_color", TemplateFieldType.Dropdown, "Favorite color", null, false, null, null, null, null, null,
            false, null, null, null, AggregationRule.None, null,
            [new TemplateFieldOption("red", "Red"), new TemplateFieldOption("red", "Red Again")], null);

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ARepeatableTableWithNoColumnsFailsValidation()
    {
        var field = new TemplateField(
            "activities", TemplateFieldType.RepeatableTable, "Activities", null, false, null, null, null, null, null,
            false, null, null, null, AggregationRule.None, null, null, null);

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("at least one column"));
    }

    [Fact]
    public void ARepeatableTableWithColumnsIsValid()
    {
        var column = new TemplateField(
            "activity_name", TemplateFieldType.ShortText, "Activity", null, true, null, null, null, null, null,
            false, null, null, null, AggregationRule.None, null, null, null);
        var field = new TemplateField(
            "activities", TemplateFieldType.RepeatableTable, "Activities", null, false, null, null, null, null, null,
            false, null, null, null, AggregationRule.None, null, null, [column]);

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ANestedRepeatableTableColumnFailsValidation()
    {
        var nestedTable = new TemplateField(
            "inner", TemplateFieldType.RepeatableTable, "Inner", null, false, null, null, null, null, null,
            false, null, null, null, AggregationRule.None, null, null, null);
        var field = new TemplateField(
            "activities", TemplateFieldType.RepeatableTable, "Activities", null, false, null, null, null, null, null,
            false, null, null, null, AggregationRule.None, null, null, [nestedTable]);

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("can't themselves be repeatable tables"));
    }

    [Fact]
    public void AComputedFieldWithNoExpressionFailsValidation()
    {
        var field = new TemplateField(
            "attendance_rate", TemplateFieldType.Computed, "Attendance rate", null, false, null, null, null, null, null,
            false, null, null, null, AggregationRule.None, null, null, null);

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("needs an expression"));
    }

    [Fact]
    public void AComputedFieldWithAnExpressionIsValid()
    {
        var field = new TemplateField(
            "attendance_rate", TemplateFieldType.Computed, "Attendance rate", null, false, null, null, null, null, null,
            false, null, null, null, AggregationRule.None, "attendance / total_members", null, null);

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void AShowIfReferencingAnUnknownFieldFailsValidation()
    {
        var field = NumberField() with { ShowIf = new TemplateShowIf("no_such_field", "Yes") };

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("unknown field"));
    }

    [Fact]
    public void AShowIfReferencingItselfFailsValidation()
    {
        var field = NumberField() with { ShowIf = new TemplateShowIf("total_members", "Yes") };

        var result = _validator.Validate(new TemplateSchema([Section(field)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("show itself"));
    }

    [Fact]
    public void AShowIfReferencingAnotherRealFieldIsValid()
    {
        var trigger = new TemplateField(
            "held_meeting", TemplateFieldType.YesNo, "Held meeting?", null, true, null, null, null, null, null,
            false, null, null, null, AggregationRule.None, null, null, null);
        var dependent = NumberField() with { ShowIf = new TemplateShowIf("held_meeting", "Yes") };

        var result = _validator.Validate(new TemplateSchema([Section(trigger, dependent)]));

        Assert.True(result.IsValid);
    }
}
