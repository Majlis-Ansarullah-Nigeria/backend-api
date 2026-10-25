namespace HRS.Domain.Admin;

/// <summary>FR-ADM-004: whether one module is on for the organization. One row per module; a module
/// without a row is on.</summary>
public sealed class FeatureFlag
{
    private FeatureFlag()
    {
    }

    public FeatureModule Module { get; private set; }

    public bool Enabled { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public static FeatureFlag Create(FeatureModule module, bool enabled) => new() { Module = module, Enabled = enabled };

    public void Set(bool enabled, Guid userId, DateTimeOffset now)
    {
        Enabled = enabled;
        UpdatedAt = now;
        UpdatedByUserId = userId;
    }
}
