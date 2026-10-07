using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Identity.Mfa;
using HRS.Domain.Access;
using HRS.Domain.Common;

namespace HRS.Application.UnitTests.Features.Identity.Mfa;

public class DisableTotpCommandHandlerTests
{
    [Fact]
    public async Task DisablesTotpWhenTheUserHasNoMandatoryRole()
    {
        var identityService = new RecordingIdentityService();
        var accessPolicy = new StubAccessPolicy { HasSecretariat = false, HasSysAdmin = false };
        var handler = new DisableTotpCommandHandler(identityService, accessPolicy);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(new DisableTotpCommand(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, identityService.DisabledForUserId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RejectsDisablingForSecretariatOrSysAdmin(bool hasSecretariat, bool hasSysAdmin)
    {
        var identityService = new RecordingIdentityService();
        var accessPolicy = new StubAccessPolicy { HasSecretariat = hasSecretariat, HasSysAdmin = hasSysAdmin };
        var handler = new DisableTotpCommandHandler(identityService, accessPolicy);

        var result = await handler.Handle(new DisableTotpCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Null(identityService.DisabledForUserId);
    }

    private sealed class StubAccessPolicy : IAccessPolicy
    {
        public required bool HasSecretariat { get; init; }

        public required bool HasSysAdmin { get; init; }

        public Task<bool> HasRoleAsync(Guid userId, RoleName role, CancellationToken cancellationToken) =>
            Task.FromResult(role switch
            {
                RoleName.Secretariat => HasSecretariat,
                RoleName.SysAdmin => HasSysAdmin,
                _ => false,
            });

        public Task<bool> HasRoleForUnitAsync(Guid userId, Guid unitId, RoleName role, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsOrganizationAdminAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingIdentityService : IIdentityService
    {
        public Guid? DisabledForUserId { get; private set; }

        public Task DisableTotpAsync(Guid userId, CancellationToken cancellationToken)
        {
            DisabledForUserId = userId;
            return Task.CompletedTask;
        }

        public Task<Result<AuthenticatedUser>> ValidateCredentialsAsync(
            string identifier, string password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AuthTokens> IssueTokensAsync(
            Guid userId, string? ipAddress, string? userAgent, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<AuthTokens>> RefreshAsync(
            string refreshToken, string? ipAddress, string? userAgent, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RevokeSessionAsync(string refreshToken, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AuthenticatedUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> RequestOtpAsync(string identifier, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<AuthenticatedUser>> VerifyOtpAsync(
            string identifier, string code, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> EmailIsRegisteredAsync(string email, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<AuthenticatedUser>> CreateUserAsync(
            string email, string name, string? phone, string password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetActiveRoleAssignmentAsync(Guid userId, Guid roleAssignmentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> CreateMfaChallengeAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<AuthenticatedUser>> VerifyMfaChallengeAsync(
            string challengeToken, string code, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TotpEnrollment> BeginTotpEnrollmentAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<string>>> ConfirmTotpEnrollmentAsync(
            Guid userId, string code, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SessionSummary>> GetActiveSessionsAsync(
            Guid userId, Guid currentSessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> RevokeSessionByIdAsync(
            Guid userId, Guid sessionId, Guid currentSessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RevokeOtherSessionsAsync(Guid userId, Guid currentSessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> RequestPasswordResetAsync(string identifier, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
