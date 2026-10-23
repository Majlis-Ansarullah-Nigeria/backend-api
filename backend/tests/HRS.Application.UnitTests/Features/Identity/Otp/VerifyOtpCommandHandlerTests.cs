using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Models;
using HRS.Application.Features.Identity.Otp;
using HRS.Application.UnitTests.Common;
using HRS.Domain.Common;

namespace HRS.Application.UnitTests.Features.Identity.Otp;

public class VerifyOtpCommandHandlerTests
{
    [Fact]
    public async Task ReturnsLoggedInWhenTheCodeIsValidAndMfaIsNotEnabled()
    {
        var user = new AuthenticatedUser(Guid.NewGuid(), "Head", "head@example.org");
        var tokens = new AuthTokens("access", DateTimeOffset.UtcNow.AddMinutes(15), "refresh", DateTimeOffset.UtcNow.AddDays(30));
        var identityService = new StubIdentityService { VerifyResult = Result.Success(user), Tokens = tokens };
        var handler = new VerifyOtpCommandHandler(identityService, new RecordingAuditLog());

        var result = await handler.Handle(new VerifyOtpCommand("head@example.org", "123456", null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var loggedIn = Assert.IsType<LoggedIn>(result.Value);
        Assert.Equal(user.Id, loggedIn.Result.UserId);
        Assert.Equal(tokens, loggedIn.Result.Tokens);
    }

    [Fact]
    public async Task ReturnsMfaChallengeRequiredWhenTheUserHasTotpEnabled()
    {
        var user = new AuthenticatedUser(Guid.NewGuid(), "Head", "head@example.org", TwoFactorEnabled: true);
        var identityService = new StubIdentityService
        {
            VerifyResult = Result.Success(user),
            ChallengeToken = "a-challenge-token",
        };
        var handler = new VerifyOtpCommandHandler(identityService, new RecordingAuditLog());

        var result = await handler.Handle(new VerifyOtpCommand("head@example.org", "123456", null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var challenge = Assert.IsType<MfaChallengeRequired>(result.Value);
        Assert.Equal("a-challenge-token", challenge.ChallengeToken);
    }

    [Fact]
    public async Task PropagatesErrorWhenTheCodeIsInvalid()
    {
        var error = Error.Unauthenticated("INVALID_OTP", "That code is invalid or has expired.");
        var identityService = new StubIdentityService { VerifyResult = Result.Failure<AuthenticatedUser>(error) };
        var handler = new VerifyOtpCommandHandler(identityService, new RecordingAuditLog());

        var result = await handler.Handle(new VerifyOtpCommand("head@example.org", "000000", null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    private sealed class StubIdentityService : IIdentityService
    {
        public required Result<AuthenticatedUser> VerifyResult { get; init; }

        public AuthTokens? Tokens { get; init; }

        public string? ChallengeToken { get; init; }

        public Task<Result<AuthenticatedUser>> ValidateCredentialsAsync(
            string identifier, string password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AuthTokens> IssueTokensAsync(
            Guid userId, string? ipAddress, string? userAgent, CancellationToken cancellationToken) =>
            Task.FromResult(Tokens!);

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
            Task.FromResult(VerifyResult);

        public Task<bool> EmailIsRegisteredAsync(string email, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<AuthenticatedUser>> CreateUserAsync(
            string email, string name, string? phone, string password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetActiveRoleAssignmentAsync(Guid userId, Guid roleAssignmentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> CreateMfaChallengeAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(ChallengeToken!);

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
