using HRS.Application.Features.Analytics;
using HRS.Application.Features.Review.Compiled;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Application.UnitTests.Features.Analytics;

public class CompiledHighlightsTests
{
    private static readonly IndicatorField Members = new("members", "Total members", TemplateFieldType.Number, null, AggregationRule.Sum);

    private static IndicatorCalculator.UnitNode Zone(string name) => new(Guid.NewGuid(), null, name, UnitLevelCode.Zone, name);

    [Fact]
    public void NamesTheHighestZoneAndTheLargestRiseAndFall()
    {
        var current = new Dictionary<string, double?> { ["Lagos"] = 1064, ["South West"] = 702, ["North West"] = 310 };
        var previous = new Dictionary<string, double?> { ["Lagos"] = 1043, ["South West"] = 702, ["North West"] = 320 };

        var highlights = CompiledReportBuilder.Highlights(
            Members, [Zone("Lagos"), Zone("South West"), Zone("North West")], z => current[z.Name], z => previous[z.Name]).ToList();

        Assert.Equal(
            [
                (CompiledHighlightKind.Highest, "Lagos", 1064d),
                (CompiledHighlightKind.LargestRise, "Lagos", 1064d),
                (CompiledHighlightKind.LargestFall, "North West", 310d),
            ],
            highlights.Select(h => (h.Kind, h.ZoneName, h.Value)));
        Assert.Equal(-10, highlights[2].Change);
    }

    [Fact]
    public void LeavesOutRisesAndFallsThatDidntHappen()
    {
        var flat = CompiledReportBuilder.Highlights(Members, [Zone("Lagos")], _ => 5, _ => 5).ToList();
        var noFigures = CompiledReportBuilder.Highlights(Members, [Zone("Lagos")], _ => null, _ => 5).ToList();

        Assert.Equal([CompiledHighlightKind.Highest], flat.Select(h => h.Kind));
        Assert.Empty(noFigures);
    }
}
