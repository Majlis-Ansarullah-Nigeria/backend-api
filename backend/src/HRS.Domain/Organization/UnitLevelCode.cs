namespace HRS.Domain.Organization;

/// <summary>
/// The fixed tree shape (hrs-domain skill): National → Zone → Dila → Muqam, with Department
/// hanging off National directly. This is a structural fact of the domain, not configurable —
/// only each level's display Name/PluralName is (see UnitLevel).
/// </summary>
public enum UnitLevelCode
{
    National,
    Zone,
    Dila,
    Muqam,
    Department,
}
