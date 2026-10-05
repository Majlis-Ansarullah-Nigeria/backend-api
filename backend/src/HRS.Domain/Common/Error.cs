using System.Diagnostics.CodeAnalysis;

namespace HRS.Domain.Common;

[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Error is the project's documented Result/Error convention (backend/CLAUDE.md, docs/architecture.md).")]
public sealed record Error(string Code, string Message, ErrorType Type, int? RetryAfterSeconds = null)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error Unauthenticated(string code, string message) => new(code, message, ErrorType.Unauthenticated);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error RateLimited(string code, string message, int retryAfterSeconds) =>
        new(code, message, ErrorType.RateLimited, retryAfterSeconds);

    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}
