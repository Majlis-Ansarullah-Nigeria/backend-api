using HRS.Domain.Organization;

namespace HRS.Domain.UnitTests.Organization;

public class UnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid ZoneLevelId = Guid.NewGuid();
    private static readonly Guid DilaLevelId = Guid.NewGuid();

    [Fact]
    public void CreateRootHasNoParentAndItsOwnCodeAsThePath()
    {
        var root = Unit.CreateRoot(Guid.NewGuid(), "NAT", "National", Guid.NewGuid(), Now);

        Assert.Null(root.ParentId);
        Assert.Equal("nat", root.Path);
        Assert.Equal("NAT", root.Code);
    }

    [Fact]
    public void CreateAppendsTheNormalizedCodeToTheParentsPath()
    {
        var root = Unit.CreateRoot(Guid.NewGuid(), "NAT", "National", Guid.NewGuid(), Now);
        var zone = Unit.Create(
            Guid.NewGuid(), "z03", "Zone 3", ZoneLevelId, root, null, null, null, null, null, Now);

        Assert.Equal(root.Id, zone.ParentId);
        Assert.Equal("nat.z03", zone.Path);
        Assert.Equal("Z03", zone.Code);
    }

    [Fact]
    public void PathNestsThroughMultipleLevels()
    {
        var root = Unit.CreateRoot(Guid.NewGuid(), "NAT", "National", Guid.NewGuid(), Now);
        var zone = Unit.Create(
            Guid.NewGuid(), "Z03", "Zone 3", ZoneLevelId, root, null, null, null, null, null, Now);
        var dila = Unit.Create(
            Guid.NewGuid(), "D12", "Dila 12", DilaLevelId, zone, null, null, null, null, null, Now);

        Assert.Equal("nat.z03.d12", dila.Path);
        Assert.Equal("Z03-D12", dila.DisplayCode());
    }

    [Fact]
    public void RenameChangesOnlyTheName()
    {
        var unit = Unit.CreateRoot(Guid.NewGuid(), "NAT", "National", Guid.NewGuid(), Now);

        unit.Rename("National HQ");

        Assert.Equal("National HQ", unit.Name);
        Assert.Equal("NAT", unit.Code);
    }

    [Fact]
    public void UpdateContactReplacesAllContactFields()
    {
        var unit = Unit.CreateRoot(Guid.NewGuid(), "NAT", "National", Guid.NewGuid(), Now);

        unit.UpdateContact("1 Main St", "+2348030000000", "nat@example.org", 6.5, 3.4);

        Assert.Equal("1 Main St", unit.Address);
        Assert.Equal("+2348030000000", unit.ContactPhone);
        Assert.Equal("nat@example.org", unit.ContactEmail);
        Assert.Equal(6.5, unit.Latitude);
        Assert.Equal(3.4, unit.Longitude);
    }

    [Fact]
    public void ArchiveThenRestoreRoundTrips()
    {
        var unit = Unit.CreateRoot(Guid.NewGuid(), "NAT", "National", Guid.NewGuid(), Now);

        unit.Archive(Now);
        Assert.True(unit.IsArchived);
        Assert.Equal(Now, unit.ArchivedAt);

        unit.Restore();
        Assert.False(unit.IsArchived);
        Assert.Null(unit.ArchivedAt);
    }
}
