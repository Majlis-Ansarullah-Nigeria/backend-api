using HRS.Domain.Admin;

namespace HRS.Application.Common.Interfaces;

/// <summary>FR-ADM-004: is a module switched on? Read once per request.</summary>
public interface IFeatureFlags
{
    Task<bool> IsEnabledAsync(FeatureModule feature, CancellationToken cancellationToken);
}

/// <summary>Marks a request that belongs to a switchable module; FeatureFlagBehavior refuses it
/// while the module is off — the server-side half of hiding a module in the UI.</summary>
public interface IRequiresFeature
{
    FeatureModule Feature { get; }
}

public sealed class FeatureDisabledException(FeatureModule module)
    : Exception($"The {module} module is switched off for this organization.")
{
    public FeatureModule Module { get; } = module;
}
