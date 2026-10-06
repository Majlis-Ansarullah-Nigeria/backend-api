namespace HRS.Application.Common.Models;

/// <summary>
/// What happens after the first factor (password or OTP) succeeds: either a session starts
/// right away, or — for a user with TOTP enabled (FR-IAM-003) — a second factor is still owed.
/// </summary>
public abstract record LoginOutcome;

public sealed record LoggedIn(AuthResult Result) : LoginOutcome;

public sealed record MfaChallengeRequired(string ChallengeToken) : LoginOutcome;
