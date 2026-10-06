namespace HRS.Domain.Organization;

/// <summary>
/// Name/PluralName are configurable per organization (hrs-domain skill: "never hard-code
/// level names in logic" — Dila/Dilaat, Muqam/Muqamaat). The tree shape itself is fixed
/// (see UnitHierarchyRules), keyed by Code, not by this row's editable display strings.
/// </summary>
public sealed class UnitLevel
{
    private UnitLevel()
    {
    }

    public UnitLevel(Guid id, UnitLevelCode code, string name, string pluralName, int depth)
    {
        Id = id;
        Code = code;
        Name = name;
        PluralName = pluralName;
        Depth = depth;
    }

    public Guid Id { get; private set; }

    public UnitLevelCode Code { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string PluralName { get; private set; } = string.Empty;

    public int Depth { get; private set; }

    public void Rename(string name, string pluralName)
    {
        Name = name;
        PluralName = pluralName;
    }
}
