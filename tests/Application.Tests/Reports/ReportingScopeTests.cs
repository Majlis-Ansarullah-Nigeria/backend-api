using FluentAssertions;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;

using Moq;
using Xunit;

namespace ManagementApi.Application.Tests.Reports;

public class ReportingScopeTests
{
    private static readonly Guid Zone1 = Guid.NewGuid();
    private static readonly Guid Zone2 = Guid.NewGuid();
    private static readonly Guid Dila1 = Guid.NewGuid(); // in Zone1
    private static readonly Guid Dila2 = Guid.NewGuid(); // in Zone2
    private static readonly Guid Muqam1 = Guid.NewGuid(); // in Dila1
    private static readonly Guid Muqam2 = Guid.NewGuid(); // in Dila2

    private static readonly ReportSubmission Muqam1Report = Report(OrganizationLevel.Muqam, Muqam1, Dila1, Zone1);
    private static readonly ReportSubmission Muqam2Report = Report(OrganizationLevel.Muqam, Muqam2, Dila2, Zone2);
    private static readonly ReportSubmission Dila1Report = Report(OrganizationLevel.Dila, Dila1, Dila1, Zone1);
    private static readonly ReportSubmission Dila2Report = Report(OrganizationLevel.Dila, Dila2, Dila2, Zone2);
    private static readonly ReportSubmission Zone1Report = Report(OrganizationLevel.Zone, Zone1, null, Zone1);

    private static readonly ReportSubmission[] All = { Muqam1Report, Muqam2Report, Dila1Report, Dila2Report, Zone1Report };

    [Theory]
    [InlineData(global::ManagementApi.Shared.Authorization.Roles.NationalAdmin)]
    [InlineData(global::ManagementApi.Shared.Authorization.Roles.SuperAdmin)]
    public void Secretariat_sees_every_report(string role)
    {
        var user = User(OrganizationLevel.Muqam, muqamId: Muqam1, role: role);

        All.AsQueryable().VisibleTo(user).Should().HaveCount(All.Length);
        ReportingScope.IsSecretariat(user).Should().BeTrue();
    }

    [Fact]
    public void Muqam_head_sees_only_their_own_muqam()
    {
        var user = User(OrganizationLevel.Muqam, muqamId: Muqam1, dilaId: Dila1, zoneId: Zone1);

        All.AsQueryable().VisibleTo(user).Should().BeEquivalentTo(new[] { Muqam1Report });
    }

    [Fact]
    public void Dila_head_sees_own_dila_and_observes_its_muqamaat()
    {
        var user = User(OrganizationLevel.Dila, dilaId: Dila1, zoneId: Zone1);

        All.AsQueryable().VisibleTo(user).Should().BeEquivalentTo(new[] { Dila1Report, Muqam1Report });
    }

    [Fact]
    public void Zone_head_sees_own_zone_and_observes_its_dilaat_but_not_muqam_reports()
    {
        var user = User(OrganizationLevel.Zone, zoneId: Zone1);

        All.AsQueryable().VisibleTo(user).Should().BeEquivalentTo(new[] { Zone1Report, Dila1Report });
    }

    [Fact]
    public void User_without_a_unit_sees_nothing()
    {
        var user = User(OrganizationLevel.Dila);

        All.AsQueryable().VisibleTo(user).Should().BeEmpty();
    }

    [Fact]
    public void Observers_do_not_own_child_reports()
    {
        var dilaHead = User(OrganizationLevel.Dila, dilaId: Dila1, zoneId: Zone1);

        ReportingScope.OwnsReport(dilaHead, Dila1Report).Should().BeTrue();
        ReportingScope.OwnsReport(dilaHead, Muqam1Report).Should().BeFalse();
        ReportingScope.IsSecretariat(dilaHead).Should().BeFalse();
    }

    [Fact]
    public void A_new_head_of_the_same_unit_owns_the_units_reports()
    {
        var newMuqamHead = User(OrganizationLevel.Muqam, muqamId: Muqam1);

        ReportingScope.OwnsReport(newMuqamHead, Muqam1Report).Should().BeTrue();
    }

    private static ReportSubmission Report(OrganizationLevel level, Guid unitId, Guid? dilaId, Guid? zoneId) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        level,
        unitId,
        level == OrganizationLevel.Muqam ? unitId : null,
        dilaId,
        zoneId,
        Guid.NewGuid(),
        "1",
        "Former head",
        null,
        "{}");

    private static ICurrentUserService User(
        OrganizationLevel level,
        Guid? muqamId = null,
        Guid? dilaId = null,
        Guid? zoneId = null,
        string? role = null)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        user.SetupGet(u => u.OrganizationLevel).Returns(level);
        user.SetupGet(u => u.MuqamId).Returns(muqamId);
        user.SetupGet(u => u.DilaId).Returns(dilaId);
        user.SetupGet(u => u.ZoneId).Returns(zoneId);
        user.Setup(u => u.IsInRole(It.IsAny<string>())).Returns<string>(r => r == role);
        return user.Object;
    }
}
