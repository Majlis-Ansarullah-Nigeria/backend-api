using HRS.Domain.Access;

namespace HRS.Domain.UnitTests.Access;

public class RoleAssignmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static RoleAssignment Create(
        RoleName role = RoleName.UnitHead,
        DelegateScope? delegateScope = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), role, delegateScope, startsAt ?? Now, endsAt, null);

    [Fact]
    public void IsActiveOnceStartedWithNoEndDate()
    {
        var assignment = Create(startsAt: Now);

        Assert.False(assignment.IsActive(Now.AddDays(-1)));
        Assert.True(assignment.IsActive(Now));
        Assert.True(assignment.IsActive(Now.AddYears(5)));
    }

    [Fact]
    public void IsNotActiveAfterItsEndDate()
    {
        var assignment = Create(startsAt: Now, endsAt: Now.AddDays(30));

        Assert.True(assignment.IsActive(Now.AddDays(29)));
        Assert.False(assignment.IsActive(Now.AddDays(30)));
        Assert.False(assignment.IsActive(Now.AddDays(31)));
    }

    [Fact]
    public void EndSetsTheEndDateToNow()
    {
        var assignment = Create(startsAt: Now);

        assignment.End(Now.AddDays(10));

        Assert.Equal(Now.AddDays(10), assignment.EndsAt);
        Assert.False(assignment.IsActive(Now.AddDays(10)));
        Assert.True(assignment.IsActive(Now.AddDays(9)));
    }

    [Fact]
    public void DelegateRoleRequiresADelegateScope()
    {
        Assert.Throws<ArgumentException>(() => Create(role: RoleName.Delegate, delegateScope: null));
    }

    [Fact]
    public void NonDelegateRoleMustNotHaveADelegateScope()
    {
        Assert.Throws<ArgumentException>(() => Create(role: RoleName.UnitHead, delegateScope: DelegateScope.DraftOnly));
    }

    [Fact]
    public void DelegateRoleWithAScopeIsValid()
    {
        var assignment = Create(role: RoleName.Delegate, delegateScope: DelegateScope.DraftAndSubmit);

        Assert.Equal(RoleName.Delegate, assignment.Role);
        Assert.Equal(DelegateScope.DraftAndSubmit, assignment.DelegateScope);
    }
}
