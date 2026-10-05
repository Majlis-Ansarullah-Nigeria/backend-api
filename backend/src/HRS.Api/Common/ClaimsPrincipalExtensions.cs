using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HRS.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return value is not null && Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Authenticated principal has no valid 'sub' claim.");
    }
}
