using HRS.Domain.Organization;
using Unit = HRS.Domain.Organization.Unit;

namespace HRS.Application.Features.Organization.Units;

public sealed record UnitSummary(
    Guid Id,
    string Code,
    string DisplayCode,
    string Name,
    Guid LevelId,
    UnitLevelCode LevelCode,
    Guid? ParentId,
    string Path,
    string? Address,
    string? ContactPhone,
    string? ContactEmail,
    double? Latitude,
    double? Longitude,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt)
{
    public static UnitSummary FromEntity(Unit unit, UnitLevelCode levelCode) => new(
        unit.Id,
        unit.Code,
        unit.DisplayCode(),
        unit.Name,
        unit.LevelId,
        levelCode,
        unit.ParentId,
        unit.Path,
        unit.Address,
        unit.ContactPhone,
        unit.ContactEmail,
        unit.Latitude,
        unit.Longitude,
        unit.CreatedAt,
        unit.ArchivedAt);
}
