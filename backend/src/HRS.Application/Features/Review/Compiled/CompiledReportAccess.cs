using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;

namespace HRS.Application.Features.Review.Compiled;

/// <summary>FR-REV-006: the Secretariat compiles and edits the national report; Leadership reads
/// and downloads it (SRS §3.2 "National dashboards and compiled report").</summary>
public static class CompiledReportAccess
{
    public sealed record Grant(bool CanView, bool CanEdit);

    public static async Task<Grant> ResolveAsync(IAccessPolicy accessPolicy, Guid userId, CancellationToken cancellationToken)
    {
        if (await accessPolicy.HasRoleAsync(userId, RoleName.Secretariat, cancellationToken))
        {
            return new Grant(true, true);
        }

        return new Grant(await accessPolicy.HasRoleAsync(userId, RoleName.Leadership, cancellationToken), false);
    }
}
