namespace HRS.Domain.Admin;

/// <summary>FR-ADM-004's switchable modules. Core submission, review, history, in-app and email
/// notifications and administration are never switchable.</summary>
public enum FeatureModule
{
    /// <summary>Compliance and indicator dashboards, the map view and Leadership's overview.</summary>
    Dashboards,

    /// <summary>The compiled national report and its exports.</summary>
    CompiledReports,

    /// <summary>Period comparison and report version history.</summary>
    ComparisonAndVersions,

    /// <summary>Browser push notifications.</summary>
    WebPush,
}
