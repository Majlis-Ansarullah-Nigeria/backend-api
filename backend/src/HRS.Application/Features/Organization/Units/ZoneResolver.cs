using HRS.Domain.Organization;

namespace HRS.Application.Features.Organization.Units;

/// <summary>Finds a unit's Zone ancestor via its materialized Path (hrs-domain: "nat.z03.d12.m04").
/// National and Department units have no Zone ancestor at all (UnitHierarchyRules: Department
/// hangs off National directly); a Zone unit's own Zone is itself. Shared by GetInboxQuery
/// (FR-REV-001) and GetDepartmentReportsQuery (FR-REV-005) — both need "which Zone is this report
/// under" for the same zone filter/display, over the same bulk-loaded (path -&gt; unit) map.</summary>
public static class ZoneResolver
{
    public static (Guid? ZoneUnitId, string? ZoneName) Resolve(
        string unitPath, UnitLevelCode unitLevel, IReadOnlyDictionary<string, Unit> unitByPath)
    {
        if (unitLevel is UnitLevelCode.National or UnitLevelCode.Department)
        {
            return (null, null);
        }

        var zonePath = unitLevel == UnitLevelCode.Zone
            ? unitPath
            : string.Join('.', unitPath.Split('.').Take(2));

        return unitByPath.TryGetValue(zonePath, out var zoneUnit) ? (zoneUnit.Id, zoneUnit.Name) : (null, null);
    }
}
