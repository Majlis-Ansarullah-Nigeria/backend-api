using HRS.Domain.Organization;

namespace HRS.Application.Features.Organization.Levels;

public sealed record UnitLevelSummary(Guid Id, UnitLevelCode Code, string Name, string PluralName, int Depth)
{
    public static UnitLevelSummary FromEntity(UnitLevel level) =>
        new(level.Id, level.Code, level.Name, level.PluralName, level.Depth);
}
