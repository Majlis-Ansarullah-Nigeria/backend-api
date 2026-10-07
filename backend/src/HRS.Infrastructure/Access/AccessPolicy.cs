using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace HRS.Infrastructure.Access;

public sealed class AccessPolicy(IAppDbContext db, IClock clock) : IAccessPolicy
{
    public Task<bool> HasRoleAsync(Guid userId, RoleName role, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        return db.RoleAssignments.AsNoTracking().AnyAsync(
            a => a.UserId == userId && a.Role == role && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now),
            cancellationToken);
    }

    public Task<bool> HasRoleForUnitAsync(Guid userId, Guid unitId, RoleName role, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        return db.RoleAssignments.AsNoTracking().AnyAsync(
            a => a.UserId == userId
                && a.UnitId == unitId
                && a.Role == role
                && a.StartsAt <= now
                && (a.EndsAt == null || a.EndsAt > now),
            cancellationToken);
    }

    public async Task<bool> IsOrganizationAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        await HasRoleAsync(userId, RoleName.Secretariat, cancellationToken)
            || await HasRoleAsync(userId, RoleName.SysAdmin, cancellationToken);

    public Task<bool> IsTemplateAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        HasRoleAsync(userId, RoleName.Secretariat, cancellationToken);
}
