using ManagementApi.Application.Common.Models;
using ManagementApi.Domain.Identity;

namespace ManagementApi.Application.Common.Interfaces;

/// <summary>Password reset and email change through ASP.NET Identity.</summary>
public interface IAccountRecoveryService
{
    /// <summary>A single-use link to the frontend reset page; expires after 1 hour.</summary>
    Task<string> CreatePasswordResetLinkAsync(ApplicationUser user);

    /// <summary>Fails with "INVALID_TOKEN" when the link has expired or was already used.</summary>
    Task<Result> ResetPasswordAsync(Guid userId, string token, string newPassword);

    Task<Result> ChangeEmailAsync(ApplicationUser user, string newEmail);
}
