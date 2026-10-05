namespace HRS.Domain.Invitations;

/// <summary>
/// A pending invite to join HRS (FR-IAM-002). Stores only a hash of the invite token.
/// Deliberately carries no unit/role reference yet — Units (M2.1) and RoleAssignment
/// (M1.4) don't exist. M2.4 wires "assign head" to issuing one of these.
/// </summary>
public sealed class Invitation
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);

    private Invitation()
    {
    }

    private Invitation(Guid id, string email, string name, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = id;
        Email = email;
        Name = name;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static Invitation Create(Guid id, string email, string name, string tokenHash, DateTimeOffset now) =>
        new(id, email, name, tokenHash, now, now.Add(DefaultLifetime));

    public bool IsValid(DateTimeOffset now) => AcceptedAt is null && RevokedAt is null && now < ExpiresAt;

    public void Accept(DateTimeOffset now) => AcceptedAt = now;

    public void Revoke(DateTimeOffset now) => RevokedAt = now;
}
