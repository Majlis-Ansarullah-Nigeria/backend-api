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

            // Get existing permissions for this role
            var existingPermissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.Permission)
                .ToListAsync();

            // Add new permissions
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
            _logger.LogInformation("Seeded {Count} permissions for role: {RoleName}", permissions.Count, roleName);
        }
    }

    private async Task SeedAdminUserAsync()
    {
        _logger.LogInformation("Checking for admin user...");

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
                FirstName = "System",
                LastName = "Administrator",
                IsActive = true,
                OrganizationLevel = Domain.Enums.OrganizationLevel.National
            };

            var result = await _userManager.CreateAsync(adminUser, adminPassword);

            if (result.Succeeded)
            {
                _logger.LogInformation("Created admin user with ChandaNo: {ChandaNo}", adminChandaNo);

                // Assign Admin role
                var roleResult = await _userManager.AddToRoleAsync(adminUser, Roles.Admin);

                if (roleResult.Succeeded)
                {
                    _logger.LogInformation("Assigned Admin role to admin user");
                }
                else
                {
                    _logger.LogError("Failed to assign Admin role. Errors: {Errors}",
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
            _logger.LogInformation("Admin user already exists");
        }
    }

    private static string GetRoleDescription(string roleName)
    {
        return roleName switch
        {
            Roles.Admin => "System administrator with full access to all features",
            Roles.Member => "Default role for all registered members",
            Roles.NationalSecretary => "National level secretary with organization-wide access",
            Roles.ZonalCoordinator => "Zone level coordinator with zone-wide access",
            Roles.NazimAala => "Dila (District) head with district-level access",
            Roles.ZaimAala => "Muqam (Local) head with local-level access",
            Roles.TajneedSecretary => "Tajneed (Membership) secretary managing member records",
            Roles.MaalSecretary => "Maal (Finance) secretary managing financial records",
            Roles.TalimSecretary => "Talim (Education) secretary managing educational programs",
            Roles.TarbiyyatSecretary => "Tarbiyyat (Training) secretary managing training programs",
            _ => $"Role: {roleName}"
        };
    }
}
