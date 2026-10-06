using FluentValidation;

namespace HRS.Application.Features.Identity.Mfa;

public sealed class ConfirmTotpEnrollmentCommandValidator : AbstractValidator<ConfirmTotpEnrollmentCommand>
{
    public ConfirmTotpEnrollmentCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}
