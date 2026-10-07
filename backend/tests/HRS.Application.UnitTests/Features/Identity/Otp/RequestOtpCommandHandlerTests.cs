using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Identity.Otp;
using HRS.Domain.Common;

namespace HRS.Application.UnitTests.Features.Identity.Otp;

public class RequestOtpCommandHandlerTests
{
    [Fact]
    public async Task DelegatesToIdentityServiceAndReturnsItsResult()
    {
        var identityService = new StubIdentityService { RequestOtpResult = Result.Success() };
        var handler = new RequestOtpCommandHandler(identityService);

        var result = await handler.Handle(new RequestOtpCommand("head@example.org"), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task PropagatesAFailureFromTheIdentityService()
    {
        var error = Error.RateLimited("OTP_REQUEST_RATE_LIMITED", "Too many code requests.", 60);
        var identityService = new StubIdentityService { RequestOtpResult = Result.Failure(error) };
        var handler = new RequestOtpCommandHandler(identityService);

        var result = await handler.Handle(new RequestOtpCommand("head@example.org"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    private sealed class StubIdentityService : IIdentityService
    {
        public required Result RequestOtpResult { get; init; }

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
            Task.FromResult(RequestOtpResult);

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

        public Task DisableTotpAsync(Guid userId, CancellationToken cancellationToken) =>
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

        public Task<IReadOnlyList<UserAccountSummary>> GetAllUsersAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> ReactivateUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AdminResetMfaAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
