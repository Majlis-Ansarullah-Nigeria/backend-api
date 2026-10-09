using HRS.Domain.Access;

namespace HRS.Application.Common.Interfaces;

/// <summary>
/// Resource-based authorization skeleton (FR-AC-001..003). Checks only an active role
/// assignment for now — once ReportType routing rules (M3) and observer scope exist, the
/// handlers that touch a report (backend/CLAUDE.md) will extend these checks rather than
/// replace them.
/// </summary>
public interface IAccessPolicy
{
    /// <summary>Whether the user currently holds an active assignment with this role, for any unit.</summary>
    Task<bool> HasRoleAsync(Guid userId, RoleName role, CancellationToken cancellationToken);

    /// <summary>Whether the user currently holds an active assignment with this role for this specific unit.</summary>
    Task<bool> HasRoleForUnitAsync(Guid userId, Guid unitId, RoleName role, CancellationToken cancellationToken);

    /// <summary>
    /// Secretariat or SysAdmin — the "can manage the organization" rule (FR-ORG-001 and friends),
    /// centralized here so every org-mutation handler doesn't duplicate the role combination.
    /// </summary>
    Task<bool> IsOrganizationAdminAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Secretariat only — "build and publish templates" (SRS 3.2 role table). Unlike organization
    /// management, SysAdmin does NOT get this; kept separate from <see cref="IsOrganizationAdminAsync"/>.
    /// </summary>
    Task<bool> IsTemplateAdminAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Secretariat only — "open, close, extend windows" (SRS 3.2 role table). Same role check as
    /// <see cref="IsTemplateAdminAsync"/> today, but named and kept separate so the two can diverge
    /// if window and template permissions are ever split.
    /// </summary>
    Task<bool> IsWindowAdminAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// UnitHead or Delegate (any DelegateScope — DraftOnly delegates can still save, just not
    /// submit later) for this specific unit (FR-SUB-002..004). Submit (M5.5) layers an additional
    /// DraftAndSubmit-scope check on top of this for delegates.
    /// </summary>
    Task<bool> CanEditReportForUnitAsync(Guid userId, Guid unitId, CancellationToken cancellationToken);

    /// <summary>UnitHead or Delegate with DraftAndSubmit scope specifically — a DraftOnly
    /// delegate can save (see <see cref="CanEditReportForUnitAsync"/>) but not submit
    /// (hrs-domain: "DELEGATE (scope DraftOnly | DraftAndSubmit, max 2 per unit)").</summary>
    Task<bool> CanSubmitReportForUnitAsync(Guid userId, Guid unitId, CancellationToken cancellationToken);
}
