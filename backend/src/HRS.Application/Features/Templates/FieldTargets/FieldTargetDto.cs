using HRS.Domain.Organization;

namespace HRS.Application.Features.Templates.FieldTargets;

public sealed record FieldTargetDto(string FieldKey, UnitLevelCode Level, decimal Value);

/// <summary>One level's target in a set request; a null Value clears that level's target.</summary>
public sealed record FieldTargetLevelValue(UnitLevelCode Level, decimal? Value);
