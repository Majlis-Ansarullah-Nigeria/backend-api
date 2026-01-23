using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Domain.Identity;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IApplicationDbContext _context;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    public async Task<ApplicationUser?> GetUserByIdAsync(Guid userId)
    {
        return await _userManager.FindByIdAsync(userId.ToString());
    }

    public async Task<ApplicationUser?> GetUserByChandaNoAsync(string chandaNo)
    {
        return await _userManager.Users
            .FirstOrDefaultAsync(u => u.ChandaNo == chandaNo);
    }

    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<Result> CreateUserAsync(ApplicationUser user, string password)
    {
        var result = await _userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            return Result.Success("User created successfully");
        }

        return Result.Failure(result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<Result> UpdateUserAsync(ApplicationUser user)
    {
        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            return Result.Success("User updated successfully");
        }

        return Result.Failure(result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<Result> DeleteUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
        {
            return Result.Failure("User not found");
        }

        var result = await _userManager.DeleteAsync(user);

        if (result.Succeeded)
        {
            return Result.Success("User deleted successfully");
        }

        return Result.Failure(result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
    {
        return await _userManager.CheckPasswordAsync(user, password);
    }

    public async Task<List<string>> GetUserRolesAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return roles.ToList();
    }

    public async Task<List<string>> GetUserPermissionsAsync(ApplicationUser user)
    {
        var roles = await GetUserRolesAsync(user);

        // SuperAdmin and NationalAdmin have all permissions
        if (roles.Contains(Roles.SuperAdmin) || roles.Contains(Roles.NationalAdmin))
        {
            return Permissions.GetAllPermissions();
        }

        var permissions = new HashSet<string>();

        foreach (var roleName in roles)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var rolePermissions = await _context.RolePermissions
                    .Where(rp => rp.RoleId == role.Id)
                    .Select(rp => rp.Permission)
                    .ToListAsync();

                foreach (var permission in rolePermissions)
                {
                    permissions.Add(permission);
                }
            }
        }

        return permissions.ToList();
    }

    public async Task<Result> AssignRolesToUserAsync(Guid userId, List<string> roles)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
        {
            return Result.Failure("User not found");
        }

        // Get current roles
        var currentRoles = await _userManager.GetRolesAsync(user);

        // Remove roles that are not in the new list (except Member role)
        var rolesToRemove = currentRoles.Where(r => !roles.Contains(r) && r != Roles.Member).ToList();
        if (rolesToRemove.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                return Result.Failure(removeResult.Errors.Select(e => e.Description).ToArray());
            }
        }

        // Add new roles
        var rolesToAdd = roles.Where(r => !currentRoles.Contains(r)).ToList();
        if (rolesToAdd.Any())
        {
            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                return Result.Failure(addResult.Errors.Select(e => e.Description).ToArray());
            }
        }

        return Result.Success("Roles assigned successfully");
    }

    public async Task<Result> RemoveRoleFromUserAsync(Guid userId, string role)
    {
        // Prevent removing the Member role
        if (role == Roles.Member)
        {
            return Result.Failure("Cannot remove the Member role");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
        {
            return Result.Failure("User not found");
        }

        var result = await _userManager.RemoveFromRoleAsync(user, role);

        if (result.Succeeded)
        {
            return Result.Success($"Role '{role}' removed successfully");
        }

        return Result.Failure(result.Errors.Select(e => e.Description).ToArray());
    }
}
