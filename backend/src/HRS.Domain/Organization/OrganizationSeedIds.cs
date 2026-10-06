namespace HRS.Domain.Organization;

/// <summary>
/// Fixed IDs for the organization seed data (the 5 UnitLevels + the single National root
/// Unit), so they're reproducible across environments and referenceable by code — and tests —
/// without a lookup. The actual INSERTs live in AppDbContext's migration-time HasData seed.
/// </summary>
public static class OrganizationSeedIds
{
    public static readonly Guid NationalLevelId = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid ZoneLevelId = new("00000000-0000-0000-0000-000000000002");
    public static readonly Guid DilaLevelId = new("00000000-0000-0000-0000-000000000003");
    public static readonly Guid MuqamLevelId = new("00000000-0000-0000-0000-000000000004");
    public static readonly Guid DepartmentLevelId = new("00000000-0000-0000-0000-000000000005");

    public static readonly Guid NationalUnitId = new("00000000-0000-0000-0000-000000000010");

    public static readonly DateTimeOffset SeedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
