using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Domain.Identity;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ManagementApi.Infrastructure.Identity;

public class TokenService : ITokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly IApplicationDbContext _context;

    public TokenService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration,
        IApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _context = context;
    }

    public async Task<string> GenerateTokenAsync(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("chandaNo", user.ChandaNo ?? string.Empty),
            new("memberId", user.MemberId?.ToString() ?? string.Empty),
            new("fullName", $"{user.FirstName} {user.LastName}".Trim()),
        };

        // Add organization context claims
        if (user.MuqamId.HasValue)
            claims.Add(new("muqamId", user.MuqamId.Value.ToString()));

        if (user.DilaId.HasValue)
            claims.Add(new("dilaId", user.DilaId.Value.ToString()));

        if (user.ZoneId.HasValue)
            claims.Add(new("zoneId", user.ZoneId.Value.ToString()));

        if (user.OrganizationLevel.HasValue)
            claims.Add(new("organizationLevel", user.OrganizationLevel.Value.ToString()));

        // Add roles
        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // Add permissions from all roles
        var permissions = new HashSet<string>();

        foreach (var roleName in roles)
        {
            // Admin has all permissions
            if (roleName == Roles.Admin)
            {
                permissions = Permissions.GetAllPermissions().ToHashSet();
                break;
            }

            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var rolePermissions = _context.RolePermissions
                    .Where(rp => rp.RoleId == role.Id)
                    .Select(rp => rp.Permission)
                    .ToList();

                foreach (var permission in rolePermissions)
                {
                    permissions.Add(permission);
                }
            }
        }

        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["JwtSettings:SecretKey"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!"));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(
                double.Parse(_configuration["JwtSettings:ExpirationHours"] ?? "24")),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
