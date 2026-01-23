using System.Security.Claims;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace ManagementApi.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
    }

    public string? UserName => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;

    public string? ChandaNo => _httpContextAccessor.HttpContext?.User?.FindFirst("chandaNo")?.Value;

    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;

    public Guid? MuqamId
    {
        get
        {
            var muqamIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("muqamId")?.Value;
            return Guid.TryParse(muqamIdClaim, out var muqamId) ? muqamId : null;
        }
    }

    public Guid? DilaId
    {
        get
        {
            var dilaIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("dilaId")?.Value;
            return Guid.TryParse(dilaIdClaim, out var dilaId) ? dilaId : null;
        }
    }

    public Guid? ZoneId
    {
        get
        {
            var zoneIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("zoneId")?.Value;
            return Guid.TryParse(zoneIdClaim, out var zoneId) ? zoneId : null;
        }
    }

    public OrganizationLevel OrganizationLevel
    {
        get
        {
            var orgLevelClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("organizationLevel")?.Value;
            return Enum.TryParse<OrganizationLevel>(orgLevelClaim, out var orgLevel)
                ? orgLevel
                : OrganizationLevel.Muqam;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role)
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;
    }

    public bool HasPermission(string permission)
    {
        return _httpContextAccessor.HttpContext?.User?
            .Claims.Any(c => c.Type == "permission" && c.Value == permission) ?? false;
    }
}
