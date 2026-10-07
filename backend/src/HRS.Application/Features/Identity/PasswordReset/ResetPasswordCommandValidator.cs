using FluentValidation;

namespace HRS.Application.Features.Identity.PasswordReset;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        // Length/digit/common-password are enforced by Identity's own policy inside
        // UserManager.ResetPasswordAsync (same approach as AcceptInvitationCommandValidator).
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}
