namespace HRS.Domain.Access;

/// <summary>
/// User × Unit × Role over time (FR-AC-002, FR-AC-003). UnitId is deliberately a bare Guid with
/// no foreign key — Units don't exist until M2.1. Creation (assigning a head) is M2.4's
/// POST /units/{id}/assignments; this milestone only builds the entity, active-assignment
/// switching, and the IAccessPolicy skeleton that will read these.
/// </summary>
public sealed class RoleAssignment
{
    private RoleAssignment()
    {
    }

    public RoleAssignment(
        Guid id,
        Guid userId,
        Guid unitId,
        RoleName role,
        DelegateScope? delegateScope,
        DateTimeOffset startsAt,
        DateTimeOffset? endsAt,
        Guid? appointedBy)
    {
        if ((role == RoleName.Delegate) != delegateScope.HasValue)
        {
            throw new ArgumentException(
                "DelegateScope must be set if and only if the role is Delegate.", nameof(delegateScope));
        }

        Id = id;
        UserId = userId;
        UnitId = unitId;
        Role = role;
        DelegateScope = delegateScope;
        StartsAt = startsAt;
        EndsAt = endsAt;
        AppointedBy = appointedBy;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid UnitId { get; private set; }

    public RoleName Role { get; private set; }

    public DelegateScope? DelegateScope { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset? EndsAt { get; private set; }

    public Guid? AppointedBy { get; private set; }

    public bool IsActive(DateTimeOffset now) => now >= StartsAt && (EndsAt is null || now < EndsAt);

    /// <summary>Ends access now, e.g. when a head is replaced (FR-AC-003).</summary>
    public void End(DateTimeOffset now) => EndsAt = now;
}
