using FluentValidation;

namespace HRS.Application.Features.Identity.Invitations;

public sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.Phone)
            .Matches(@"^\+?[0-9 ()-]{7,20}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Enter a valid phone number.");
        RuleFor(x => x.AgreedToPrivacyNotice)
            .Equal(true)
            .WithMessage("You must agree to the privacy notice.");
    }
}
