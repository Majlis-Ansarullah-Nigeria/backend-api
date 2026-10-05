using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Identity.Login;
using HRS.Domain.Common;

namespace HRS.Application.UnitTests.Features.Identity.Login;

public class LoginCommandHandlerTests
{
    [Fact]
    public async Task ReturnsLoginResultWhenCredentialsAreValid()
    {
        var user = new AuthenticatedUser(Guid.NewGuid(), "Head", "head@example.org");
        var tokens = new AuthTokens("access", DateTimeOffset.UtcNow.AddMinutes(15), "refresh", DateTimeOffset.UtcNow.AddDays(30));
        var identityService = new StubIdentityService { ValidateResult = Result.Success(user), Tokens = tokens };
        var handler = new LoginCommandHandler(identityService);

        var result = await handler.Handle(new LoginCommand("head@example.org", "password", null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal(tokens, result.Value.Tokens);
    }

    [Fact]
    public async Task PropagatesErrorWhenCredentialsAreInvalid()
    {
        var error = Error.Unauthenticated("INVALID_CREDENTIALS", "Invalid email/phone or password.");
        var identityService = new StubIdentityService { ValidateResult = Result.Failure<AuthenticatedUser>(error) };
        var handler = new LoginCommandHandler(identityService);

        var result = await handler.Handle(new LoginCommand("head@example.org", "wrong", null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    private sealed class StubIdentityService : IIdentityService
    {
        public required Result<AuthenticatedUser> ValidateResult { get; init; }

        public AuthTokens? Tokens { get; init; }

        public Task<Result<AuthenticatedUser>> ValidateCredentialsAsync(
            string identifier, string password, CancellationToken cancellationToken) =>
            Task.FromResult(ValidateResult);

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
            throw new NotSupportedException();
    }
}
