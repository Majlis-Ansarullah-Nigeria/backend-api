using HRS.Application.Common.Interfaces;

namespace HRS.Application.Features.Audit;

/// <summary>FR-AUD-001's login entries. A failure records the email or phone that was typed —
/// there may be no such user — trimmed, and never the password or code.</summary>
public static class SignInAudit
{
    private const int MaxIdentifierLength = 256;

    public static AuditEntry Succeeded(Guid userId, string summary) =>
        new(AuditActions.SignedIn, userId, "user", userId.ToString(), summary);

    public static AuditEntry Failed(string? identifier, string summary)
    {
        var typed = identifier?.Trim();
        if (typed is { Length: > MaxIdentifierLength })
        {
            typed = typed[..MaxIdentifierLength];
        }

        return new AuditEntry(AuditActions.SignInFailed, null, "user", null, summary, ActorEmail: typed);
    }
}
