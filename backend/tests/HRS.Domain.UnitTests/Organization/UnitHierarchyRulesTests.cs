using HRS.Domain.Organization;

namespace HRS.Domain.UnitTests.Organization;

public class UnitHierarchyRulesTests
{
    [Theory]
    [InlineData(UnitLevelCode.Zone, UnitLevelCode.National, true)]
    [InlineData(UnitLevelCode.Dila, UnitLevelCode.Zone, true)]
    [InlineData(UnitLevelCode.Muqam, UnitLevelCode.Dila, true)]
    [InlineData(UnitLevelCode.Department, UnitLevelCode.National, true)]
    [InlineData(UnitLevelCode.Zone, UnitLevelCode.Zone, false)]
    [InlineData(UnitLevelCode.Dila, UnitLevelCode.National, false)]
    [InlineData(UnitLevelCode.Muqam, UnitLevelCode.Zone, false)]
    [InlineData(UnitLevelCode.Department, UnitLevelCode.Zone, false)]
    [InlineData(UnitLevelCode.National, UnitLevelCode.National, false)]
    public void CanBeChildOfMatchesTheFixedTreeShape(UnitLevelCode child, UnitLevelCode parent, bool expected)
    {
        Assert.Equal(expected, UnitHierarchyRules.CanBeChildOf(child, parent));
    }
}
