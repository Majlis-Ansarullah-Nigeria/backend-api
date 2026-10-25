using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Identity.CurrentUser;

public sealed record GetMeQuery(Guid UserId) : IRequest<Result<MeProfile>>;

public sealed record MeProfile(
    Guid Id,
    string Name,
    string Email,
    Guid? ActiveRoleAssignmentId,
    IReadOnlyList<RoleAssignmentSummary> RoleAssignments,
    bool MfaEnabled,
    bool MfaRequiredByRole);

/// <summary>One active assignment, with the unit's name and level so the app shell's "Acting as"
/// card and unit switcher can label it ("Agodi Muqam · Muqam Head") without another request.
/// Level names are organization data (Dila/Muqam), never hard-coded.</summary>
public sealed record RoleAssignmentSummary(
    Guid Id,
    Guid UnitId,
    RoleName Role,
    DelegateScope? DelegateScope,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string UnitName,
    UnitLevelCode UnitLevel,
    string UnitLevelName);

public sealed class GetMeQueryHandler(IIdentityService identityService, IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetMeQuery, Result<MeProfile>>
{
    public async ValueTask<Result<MeProfile>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var user = await identityService.GetUserAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<MeProfile>(Error.NotFound("USER_NOT_FOUND", "User not found."));
        }

        var now = clock.UtcNow;

        // Only currently-active assignments — the unit switcher can't offer one whose term
        // hasn't started or has already ended.
        var assignments = await db.RoleAssignments
            .AsNoTracking()
            .Where(a => a.UserId == request.UserId && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .OrderBy(a => a.StartsAt)
            .Join(db.Units.AsNoTracking(), a => a.UnitId, u => u.Id, (a, u) => new { Assignment = a, Unit = u })
            .Join(db.UnitLevels.AsNoTracking(), x => x.Unit.LevelId, l => l.Id, (x, l) => new RoleAssignmentSummary(
                x.Assignment.Id, x.Assignment.UnitId, x.Assignment.Role, x.Assignment.DelegateScope,
                x.Assignment.StartsAt, x.Assignment.EndsAt, x.Unit.Name, l.Code, l.Name))
            .ToListAsync(cancellationToken);

        // FR-IAM-003: mandatory for Secretariat/Admin. A user who hasn't enrolled yet still
        // logs in normally (see DisableTotpCommandHandler) — the frontend uses this flag to
        // force them to the setup screen instead of blocking them out entirely.
        var mfaRequiredByRole = await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken)
            || await accessPolicy.HasRoleAsync(request.UserId, RoleName.SysAdmin, cancellationToken);

        return new MeProfile(
            user.Id, user.Name, user.Email, user.ActiveRoleAssignmentId, assignments, user.TwoFactorEnabled, mfaRequiredByRole);
    }
}
