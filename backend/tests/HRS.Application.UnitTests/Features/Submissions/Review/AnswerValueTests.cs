using System.Text.Json;
using HRS.Application.Features.Submissions.Review;

namespace HRS.Application.UnitTests.Features.Submissions.Review;

public class AnswerValueTests
{
    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("hello", true)]
    public void HasValueOnRawClrValues(object? value, bool expected) => Assert.Equal(expected, AnswerValue.HasValue(value));

    [Fact]
    public void HasValueIsFalseForAnEmptyArrayAndTrueForANonEmptyOne()
    {
        Assert.False(AnswerValue.HasValue(Json("[]")));
        Assert.True(AnswerValue.HasValue(Json("""["a"]""")));
    }

    [Fact]
    public void HasValueIsFalseForAJsonNullAndAnAllEmptyObject()
    {
        Assert.False(AnswerValue.HasValue(Json("null")));
        Assert.False(AnswerValue.HasValue(Json("""{"start":"","end":""}""")));
        Assert.True(AnswerValue.HasValue(Json("""{"start":"2026-01-01","end":""}""")));
    }

    [Fact]
    public void ToComparableStringUnwrapsJsonBooleans()
    {
        Assert.Equal("true", AnswerValue.ToComparableString(Json("true")));
        Assert.Equal("false", AnswerValue.ToComparableString(Json("false")));
        Assert.Equal("true", AnswerValue.ToComparableString(true));
    }

    [Fact]
    public void ToComparableStringUnwrapsAJsonStringAndTrims()
    {
        Assert.Equal("hello", AnswerValue.ToComparableString(Json("\"  hello  \"")));
    }

    [Fact]
    public void ToDoubleOrNullParsesANumberFromRawAndJsonForms()
    {
        Assert.Equal(42.5, AnswerValue.ToDoubleOrNull(42.5));
        Assert.Equal(42.5, AnswerValue.ToDoubleOrNull(Json("42.5")));
        Assert.Equal(42.5, AnswerValue.ToDoubleOrNull(Json("\"42.5\"")));
    }

    [Fact]
    public void ToDoubleOrNullIsNullForNonNumericValues()
    {
        Assert.Null(AnswerValue.ToDoubleOrNull(null));
        Assert.Null(AnswerValue.ToDoubleOrNull(Json("\"not a number\"")));
    }
}
