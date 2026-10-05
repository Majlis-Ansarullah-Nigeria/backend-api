using FluentValidation;

namespace HRS.Application.Features.Identity.Otp;

public sealed class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}
