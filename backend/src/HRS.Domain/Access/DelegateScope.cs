namespace HRS.Domain.Access;

/// <summary>Only meaningful when RoleAssignment.Role is RoleName.Delegate (FR-AC-004).</summary>
public enum DelegateScope
{
    DraftOnly,
    DraftAndSubmit,
}
