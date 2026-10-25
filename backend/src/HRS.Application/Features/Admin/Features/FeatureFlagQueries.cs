using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Access;
using HRS.Domain.Admin;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Admin.Features;

public sealed record FeatureFlagDto(FeatureModule Module, bool Enabled);

public sealed record FeatureFlagsView(IReadOnlyList<FeatureFlagDto> Flags, bool CanEdit);

/// <summary>FR-ADM-004: every module's state. Every signed-in user reads them — the UI hides what's off.</summary>
public sealed record GetFeatureFlagsQuery(Guid UserId) : IRequest<Result<FeatureFlagsView>>;

public sealed class GetFeatureFlagsQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy) : IRequestHandler<GetFeatureFlagsQuery, Result<FeatureFlagsView>>
{
    public async ValueTask<Result<FeatureFlagsView>> Handle(GetFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        var stored = await db.FeatureFlags.AsNoTracking().ToDictionaryAsync(f => f.Module, f => f.Enabled, cancellationToken);
        var flags = Enum.GetValues<FeatureModule>().Select(m => new FeatureFlagDto(m, stored.GetValueOrDefault(m, true))).ToList();
        return Result.Success(new FeatureFlagsView(flags, await accessPolicy.HasRoleAsync(request.UserId, RoleName.SysAdmin, cancellationToken)));
    }
}

/// <summary>FR-ADM-004: switches a module on or off. System administrator only; audited.</summary>
public sealed record SetFeatureFlagCommand(Guid UserId, FeatureModule Module, bool Enabled) : IRequest<Result<FeatureFlagDto>>;

public sealed class SetFeatureFlagCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IAuditLog auditLog)
    : IRequestHandler<SetFeatureFlagCommand, Result<FeatureFlagDto>>
{
    public async ValueTask<Result<FeatureFlagDto>> Handle(SetFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.SysAdmin, cancellationToken))
        {
            return Result.Failure<FeatureFlagDto>(Error.Forbidden("FORBIDDEN", "Only the system administrator can switch modules."));
        }

        if (!Enum.IsDefined(request.Module))
        {
            return Result.Failure<FeatureFlagDto>(Error.Validation("UNKNOWN_MODULE", "There's no such module."));
        }

        var flag = await db.FeatureFlags.FirstOrDefaultAsync(f => f.Module == request.Module, cancellationToken);
        var before = flag?.Enabled ?? true;
        if (flag is null)
        {
            flag = FeatureFlag.Create(request.Module, true);
            db.FeatureFlags.Add(flag);
        }

        flag.Set(request.Enabled, request.UserId, clock.UtcNow);
        auditLog.Add(new AuditEntry(
            AuditActions.FeatureToggled, request.UserId, "feature_flag", request.Module.ToString(),
            $"{(request.Enabled ? "Switched on" : "Switched off")} {request.Module}",
            Before: new { Enabled = before }, After: new { Enabled = request.Enabled }));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new FeatureFlagDto(flag.Module, flag.Enabled));
    }
}
