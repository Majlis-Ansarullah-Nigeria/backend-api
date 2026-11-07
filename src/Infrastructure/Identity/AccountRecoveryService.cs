using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace ManagementApi.Infrastructure.Identity;

public class AccountRecoveryService : IAccountRecoveryService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public AccountRecoveryService(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    public async Task<string> CreatePasswordResetLinkAsync(ApplicationUser user)
    {
        // Identity tokens are tied to the security stamp, so a link stops working once the password changes.
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var frontend = (_configuration["App:FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');
        return $"{frontend}/reset-password?id={user.Id}&token={encoded}";
    }

    public async Task<Result> ResetPasswordAsync(Guid userId, string token, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return Result.Failure(InvalidToken);
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return Result.Failure(InvalidToken);
        }

        var result = await _userManager.ResetPasswordAsync(user, decoded, newPassword);
        if (result.Succeeded)
        {
            // A forgotten password often follows failed sign-ins; let the member in straight away.
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
            return Result.Success();
        }

        return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
            ? Result.Failure(InvalidToken)
            : Result.Failure(result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<Result> ChangeEmailAsync(ApplicationUser user, string newEmail)
    {
        var result = await _userManager.SetEmailAsync(user, newEmail);
        if (!result.Succeeded)
        {
            return Result.Failure(result.Errors.Select(e => e.Description).ToArray());
        }

        // Approved by the Secretariat, so treat the address as confirmed.
        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);
        return Result.Success();
    }

    private const string InvalidToken = "INVALID_TOKEN: This link has expired or was already used. Request a new one.";
}
