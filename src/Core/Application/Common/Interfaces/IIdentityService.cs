using ManagementApi.Application.Common.Models;
using ManagementApi.Domain.Identity;

namespace ManagementApi.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<ApplicationUser?> GetUserByIdAsync(Guid userId);
    Task<ApplicationUser?> GetUserByChandaNoAsync(string chandaNo);
    Task<ApplicationUser?> GetUserByEmailAsync(string email);
    Task<Result> CreateUserAsync(ApplicationUser user, string password);
    Task<Result> UpdateUserAsync(ApplicationUser user);
    Task<Result> DeleteUserAsync(Guid userId);
    Task<bool> CheckPasswordAsync(ApplicationUser user, string password);
    Task<List<string>> GetUserRolesAsync(ApplicationUser user);
    Task<List<string>> GetUserPermissionsAsync(ApplicationUser user);
    Task<Result> AssignRolesToUserAsync(Guid userId, List<string> roles);
    Task<Result> RemoveRoleFromUserAsync(Guid userId, string role);
}
