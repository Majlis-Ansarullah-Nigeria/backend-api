using Microsoft.AspNetCore.Identity;

namespace HRS.Infrastructure.Identity;

/// <summary>
/// Rejects an obviously common password, matching the "Not a common password" chip on the
/// set-password screen (FR-IAM-002 mockup). Runs for every password Identity sets — login
/// (M1.1), invite accept (M1.3), reset (M1.6) — since it's wired as an IPasswordValidator.
/// A short curated denylist, not a HaveIBeenPwned-style breach check — that's future hardening.
/// </summary>
public sealed class CommonPasswordValidator : IPasswordValidator<ApplicationUser>
{
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password1234",
        "password123!",
        "12345678901234",
        "123456789012",
        "qwertyuiop123",
        "letmein123456",
        "welcome123456",
        "iloveyou12345",
        "administrator1",
        "changeme12345",
        "correcthorse12",
        "abcdefghijklmn",
        "trustno1234567",
        "football123456",
        "baseball123456",
        "superman123456",
        "princess123456",
        "sunshine123456",
        "dragonball1234",
        "whateveriwant1",
    };

    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password) =>
        Task.FromResult(password is not null && CommonPasswords.Contains(password)
            ? IdentityResult.Failed(new IdentityError
            {
                Code = "CommonPassword",
                Description = "That password is too common. Choose something less predictable.",
            })
            : IdentityResult.Success);
}
