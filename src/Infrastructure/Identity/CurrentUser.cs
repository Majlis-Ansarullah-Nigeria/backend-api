using System.Security.Claims;
using ManagementApi.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ManagementApi.Infrastructure.Identity;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    public string? GetUserEmail()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;
    }

    public string? GetChandaNo()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst("ChandaNo")?.Value;
    }

    public Guid? GetMuqamId()
    {
        var muqamIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("MuqamId")?.Value;
        return Guid.TryParse(muqamIdClaim, out var muqamId) ? muqamId : null;
    }

    public Guid? GetDilaId()
    {
        var dilaIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("DilaId")?.Value;
        return Guid.TryParse(dilaIdClaim, out var dilaId) ? dilaId : null;
    }

    public Guid? GetZoneId()
    {
        var zoneIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("ZoneId")?.Value;
        return Guid.TryParse(zoneIdClaim, out var zoneId) ? zoneId : null;
    }

    public bool IsAuthenticated()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }

    public bool IsInRole(string role)
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;
    }
}
