using FluentValidation;

namespace HRS.Application.Features.Identity.Otp;

public sealed class RequestOtpCommandValidator : AbstractValidator<RequestOtpCommand>
{
    public RequestOtpCommandValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty();
    }
}
