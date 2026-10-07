using System.Text.Json;
using HRS.Domain.Templates.Expressions;

namespace HRS.Domain.UnitTests.Templates.Expressions;

/// <summary>Drives shared/expression-fixtures.json — the frontend's Vitest suite runs the exact
/// same file, so these two test runs are the cross-language parity guarantee FR-TPL-005 asks
/// for: both evaluators must agree on every case.</summary>
public class ExpressionEvaluatorFixtureTests
{
    public static IEnumerable<object[]> Fixtures()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "expression-fixtures.json");
        var json = File.ReadAllText(path);
        var raw = JsonSerializer.Deserialize<List<RawFixture>>(json, JsonSerializerOptions.Web)!;

        return raw.Select(f => new object[]
        {
            new ExpressionFixture(
                f.Name,
                f.Expression,
                f.Values.ToDictionary(kv => kv.Key, kv => ToClrValue(kv.Value)),
                f.Expected,
                f.ExpectError),
        });
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void FixtureProducesTheExpectedResult(ExpressionFixture fixture)
    {
        var result = ExpressionEvaluator.Evaluate(fixture.Expression, fixture.Values);

        if (fixture.ExpectError)
        {
            Assert.True(result.IsFailure, $"{fixture.Name}: expected a parse error but evaluation succeeded.");
        }
        else
        {
            Assert.True(result.IsSuccess, $"{fixture.Name}: expected success but got '{result.Error.Message}'.");
            Assert.Equal(fixture.Expected!.Value, result.Value, precision: 6);
        }
    }

    private static object? ToClrValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),
        _ => null,
    };

    private sealed record RawFixture(string Name, string Expression, Dictionary<string, JsonElement> Values, double? Expected, bool ExpectError);

    public sealed record ExpressionFixture(
        string Name, string Expression, IReadOnlyDictionary<string, object?> Values, double? Expected, bool ExpectError);
}
