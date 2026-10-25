using HRS.Application.Common.Interfaces;
using HRS.Domain.Admin;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Admin.Features;

public sealed class FeatureFlags(IAppDbContext db) : IFeatureFlags
{
    private Dictionary<FeatureModule, bool>? _flags;

    public async Task<bool> IsEnabledAsync(FeatureModule feature, CancellationToken cancellationToken)
    {
        _flags ??= await db.FeatureFlags.AsNoTracking().ToDictionaryAsync(f => f.Module, f => f.Enabled, cancellationToken);
        return _flags.GetValueOrDefault(feature, true);
    }
}
