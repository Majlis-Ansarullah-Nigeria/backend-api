using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Identity.Otp;
using HRS.Domain.Common;

namespace HRS.Application.UnitTests.Features.Identity.Otp;

public class VerifyOtpCommandHandlerTests
{
    [Fact]
    public async Task ReturnsAuthResultWhenTheCodeIsValid()
    {
        var user = new AuthenticatedUser(Guid.NewGuid(), "Head", "head@example.org");
        var tokens = new AuthTokens("access", DateTimeOffset.UtcNow.AddMinutes(15), "refresh", DateTimeOffset.UtcNow.AddDays(30));
        var identityService = new StubIdentityService { VerifyResult = Result.Success(user), Tokens = tokens };
        var handler = new VerifyOtpCommandHandler(identityService);

        var result = await handler.Handle(new VerifyOtpCommand("head@example.org", "123456", null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal(tokens, result.Value.Tokens);
    }

    [Fact]
    public async Task PropagatesErrorWhenTheCodeIsInvalid()
    {
        var error = Error.Unauthenticated("INVALID_OTP", "That code is invalid or has expired.");
        var identityService = new StubIdentityService { VerifyResult = Result.Failure<AuthenticatedUser>(error) };
        var handler = new VerifyOtpCommandHandler(identityService);

        var result = await handler.Handle(new VerifyOtpCommand("head@example.org", "000000", null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    private sealed class StubIdentityService : IIdentityService
    {
        public required Result<AuthenticatedUser> VerifyResult { get; init; }

        public AuthTokens? Tokens { get; init; }

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
    }
}
