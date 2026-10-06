namespace HRS.Domain.Organization;

/// <summary>Which level may parent which. National has no parent — it's the single seeded root.</summary>
public static class UnitHierarchyRules
{
    private static readonly Dictionary<UnitLevelCode, UnitLevelCode?> AllowedParent = new()
    {
        [UnitLevelCode.National] = null,
        [UnitLevelCode.Zone] = UnitLevelCode.National,
        [UnitLevelCode.Dila] = UnitLevelCode.Zone,
        [UnitLevelCode.Muqam] = UnitLevelCode.Dila,
        [UnitLevelCode.Department] = UnitLevelCode.National,
    };

    public static bool CanBeChildOf(UnitLevelCode childLevel, UnitLevelCode parentLevel) =>
        AllowedParent[childLevel] == parentLevel;
}
