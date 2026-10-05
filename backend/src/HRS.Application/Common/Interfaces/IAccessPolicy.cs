using HRS.Domain.Access;

namespace HRS.Application.Common.Interfaces;

/// <summary>
/// Resource-based authorization skeleton (FR-AC-001..003). Checks only an active role
/// assignment for now — once Units (M2.1), ReportType routing rules (M3) and observer
/// scope exist, the handlers that touch a report or unit (backend/CLAUDE.md) will extend
/// these checks rather than replace them.
/// </summary>
public interface IAccessPolicy
{
    /// <summary>Whether the user currently holds an active assignment with this role, for any unit.</summary>
    Task<bool> HasRoleAsync(Guid userId, RoleName role, CancellationToken cancellationToken);

    /// <summary>Whether the user currently holds an active assignment with this role for this specific unit.</summary>
    Task<bool> HasRoleForUnitAsync(Guid userId, Guid unitId, RoleName role, CancellationToken cancellationToken);
}
