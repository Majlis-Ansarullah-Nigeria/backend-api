using FluentValidation;

namespace HRS.Application.Features.Identity.PasswordReset;

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty();
    }
}
