using HRS.Domain.Invitations;

namespace HRS.Domain.UnitTests.Invitations;

public class InvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateSetsAnExpiryOfSevenDaysFromNow()
    {
        var invitation = Invitation.Create(Guid.NewGuid(), "head@example.org", "Head", "hash", Now);

        Assert.Equal(Now, invitation.CreatedAt);
        Assert.Equal(Now.AddDays(7), invitation.ExpiresAt);
        Assert.Null(invitation.AcceptedAt);
        Assert.Null(invitation.RevokedAt);
    }

    [Fact]
    public void IsValidWhileUnexpiredUnacceptedAndUnrevoked()
    {
        var invitation = Invitation.Create(Guid.NewGuid(), "head@example.org", "Head", "hash", Now);

        Assert.True(invitation.IsValid(Now));
        Assert.True(invitation.IsValid(Now.AddDays(6)));
    }

    [Fact]
    public void IsNotValidOnceExpired()
    {
        var invitation = Invitation.Create(Guid.NewGuid(), "head@example.org", "Head", "hash", Now);

        Assert.False(invitation.IsValid(Now.AddDays(7)));
        Assert.False(invitation.IsValid(Now.AddDays(8)));
    }

    [Fact]
    public void IsNotValidOnceAccepted()
    {
        var invitation = Invitation.Create(Guid.NewGuid(), "head@example.org", "Head", "hash", Now);

        invitation.Accept(Now.AddHours(1));

        Assert.Equal(Now.AddHours(1), invitation.AcceptedAt);
        Assert.False(invitation.IsValid(Now.AddHours(2)));
    }

    [Fact]
    public void IsNotValidOnceRevoked()
    {
        var invitation = Invitation.Create(Guid.NewGuid(), "head@example.org", "Head", "hash", Now);

        invitation.Revoke(Now.AddHours(1));

        Assert.Equal(Now.AddHours(1), invitation.RevokedAt);
        Assert.False(invitation.IsValid(Now.AddHours(2)));
    }
}
