using HRS.Domain.Organization;
using Unit = HRS.Domain.Organization.Unit;

namespace HRS.Application.Features.Organization.Units;

public sealed record UnitTreeNode(
    Guid Id,
    string Code,
    string DisplayCode,
    string Name,
    Guid LevelId,
    UnitLevelCode LevelCode,
    Guid? ParentId,
    string Path,
    DateTimeOffset? ArchivedAt,
    string? HeadName,
    bool HasVacantHead)
{
    public static UnitTreeNode FromEntity(Unit unit, UnitLevelCode levelCode, string? headName) => new(
        unit.Id,
        unit.Code,
        unit.DisplayCode(),
        unit.Name,
        unit.LevelId,
        levelCode,
        unit.ParentId,
        unit.Path,
        unit.ArchivedAt,
        headName,
        HasVacantHead: headName is null);
}
