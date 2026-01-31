using ManagementApi.Domain.Identity;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ManagementApi.Infrastructure.Persistence.Seeders;

public class DatabaseSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            await SeedRolesAsync();
            await SeedRolePermissionsAsync();
            await SeedAdminUserAsync();

            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }

    private async Task SeedRolesAsync()
    {
        _logger.LogInformation("Seeding roles...");

        var defaultRoles = Roles.GetDefaultRoles();

        foreach (var roleName in defaultRoles)
        {
            var roleExists = await _roleManager.RoleExistsAsync(roleName);

            if (!roleExists)
            {
                var role = new ApplicationRole
                {
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant(),
                    Description = GetRoleDescription(roleName)
                };

                var result = await _roleManager.CreateAsync(role);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Created role: {RoleName}", roleName);
                }
                else
                {
                    _logger.LogError("Failed to create role: {RoleName}. Errors: {Errors}",
                        roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    private async Task SeedRolePermissionsAsync()
    {
        _logger.LogInformation("Seeding role permissions...");

        var rolePermissionsMap = Roles.GetDefaultRolePermissions();

        foreach (var (roleName, permissions) in rolePermissionsMap)
        {
            var role = await _roleManager.FindByNameAsync(roleName);

            if (role == null)
            {
                _logger.LogWarning("Role not found: {RoleName}", roleName);
                continue;
            }

            // Get existing permission records for this role
            var existingPermissionRecords = await _context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .ToListAsync();

            var existingPermissions = existingPermissionRecords
                .Select(rp => rp.Permission)
                .ToList();

            // REMOVE permissions that are no longer in the role's permission list
            var permissionsToRemove = existingPermissionRecords
                .Where(rp => !permissions.Contains(rp.Permission))
                .ToList();

            if (permissionsToRemove.Any())
            {
                _context.RolePermissions.RemoveRange(permissionsToRemove);
                _logger.LogInformation("Removed {Count} obsolete permissions from role: {RoleName}",
                    permissionsToRemove.Count, roleName);
            }

            // ADD new permissions
            foreach (var permission in permissions)
            {
                if (!existingPermissions.Contains(permission))
                {
                    var rolePermission = new ApplicationRolePermission
                    {
                        RoleId = role.Id,
                        Permission = permission
                    };

                    _context.RolePermissions.Add(rolePermission);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Synced {Count} permissions for role: {RoleName}", permissions.Count, roleName);
        }
    }

    private async Task SeedAdminUserAsync()
    {
        _logger.LogInformation("Checking for admin users...");

        // Seed SuperAdmin user
        const string superAdminChandaNo = "SUPERADMIN";
        const string superAdminPassword = "SuperAdmin@123";

        var superAdminUser = await _userManager.Users.FirstOrDefaultAsync(u => u.ChandaNo == superAdminChandaNo);

        if (superAdminUser == null)
        {
            superAdminUser = new ApplicationUser
            {
                UserName = superAdminChandaNo,
                Email = "superadmin@majlisansarullah.ng",
                EmailConfirmed = true,
                ChandaNo = superAdminChandaNo,
                FirstName = "Super",
                LastName = "Administrator",
                IsActive = true,
                OrganizationLevel = Domain.Enums.OrganizationLevel.National
            };

            var result = await _userManager.CreateAsync(superAdminUser, superAdminPassword);

            if (result.Succeeded)
            {
                _logger.LogInformation("Created SuperAdmin user with ChandaNo: {ChandaNo}", superAdminChandaNo);

                var roleResult = await _userManager.AddToRoleAsync(superAdminUser, Roles.SuperAdmin);

                if (roleResult.Succeeded)
                {
                    _logger.LogInformation("Assigned SuperAdmin role to superadmin user");
                }
                else
                {
                    _logger.LogError("Failed to assign SuperAdmin role. Errors: {Errors}",
                        string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                _logger.LogError("Failed to create superadmin user. Errors: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            _logger.LogInformation("SuperAdmin user already exists");
        }

        // Seed NationalAdmin user
        const string adminChandaNo = "ADMIN001";
        const string adminPassword = "Admin@123";

        var adminUser = await _userManager.Users.FirstOrDefaultAsync(u => u.ChandaNo == adminChandaNo);

        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminChandaNo,
                Email = "admin@majlisansarullah.ng",
                EmailConfirmed = true,
                ChandaNo = adminChandaNo,
                FirstName = "National",
                LastName = "Administrator",
                IsActive = true,
                OrganizationLevel = Domain.Enums.OrganizationLevel.National
            };

            var result = await _userManager.CreateAsync(adminUser, adminPassword);

            if (result.Succeeded)
            {
                _logger.LogInformation("Created NationalAdmin user with ChandaNo: {ChandaNo}", adminChandaNo);

                var roleResult = await _userManager.AddToRoleAsync(adminUser, Roles.NationalAdmin);

                if (roleResult.Succeeded)
                {
                    _logger.LogInformation("Assigned NationalAdmin role to admin user");
                }
                else
                {
                    _logger.LogError("Failed to assign NationalAdmin role. Errors: {Errors}",
                        string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                _logger.LogError("Failed to create admin user. Errors: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            _logger.LogInformation("NationalAdmin user already exists");
        }
    }

    private static string GetRoleDescription(string roleName)
    {
        return roleName switch
        {
            // System Roles
            Roles.SuperAdmin => "Super Administrator - Technical administrator with all system permissions",

            // National Level
            Roles.NationalAdmin => "National Administrator - Full administrative access nationwide",
            Roles.NationalSecretary => "National Secretary - Administrative support with view and export access nationwide",

            // Zone Level
            Roles.ZoneNazim => "Zone Nazim - Regional leadership overseeing multiple Dilas in a Zone",
            Roles.ZoneSecretary => "Zone Secretary - Administrative support for Zone Nazim with view-only access",

            // Dila Level
            Roles.NazimAla => "Nazim Ala - Dila Coordinator managing multiple Muqams within a Dila",
            Roles.DilaSecretary => "Dila Secretary - Administrative support for Nazim Ala with view-only access",

            // Muqam Level
            Roles.ZaimAla => "Zaim Ala - Muqam Leader managing local Muqam activities and members",
            Roles.MuqamSecretary => "Muqam Secretary - Administrative support for Zaim Ala with view-only access",

            // Default Role
            Roles.Member => "Member - Default role for registered users without organizational position",

            // Legacy Roles (kept for backward compatibility)
#pragma warning disable CS0618 // Type or member is obsolete
            Roles.NazimAala => "Nazim A'ala (Legacy) - Use NazimAla instead",
            Roles.ZaimAala => "Zaim A'ala (Legacy) - Use ZaimAla instead",
            Roles.ZonalCoordinator => "Zonal Coordinator (Legacy) - Use ZoneNazim instead",
#pragma warning restore CS0618 // Type or member is obsolete

            _ => $"Role: {roleName}"
        };
    }
}
