using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Organizations.DTOs;
using ManagementApi.Domain.Entities;
using MediatR;

namespace ManagementApi.Application.Organizations.Commands;

public record CreateMuqamCommand(CreateMuqamRequest Request) : IRequest<Result<Guid>>;

public class CreateMuqamCommandValidator : AbstractValidator<CreateMuqamCommand>
{
    public CreateMuqamCommandValidator()
    {
        RuleFor(x => x.Request.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Request.Code)
            .MaximumLength(50).WithMessage("Code must not exceed 50 characters");

        RuleFor(x => x.Request.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Request.Email))
            .WithMessage("Invalid email address");
    }
}

public class CreateMuqamCommandHandler : IRequestHandler<CreateMuqamCommand, Result<Guid>>
{
    private readonly IRepository<Muqam> _repository;

    public CreateMuqamCommandHandler(IRepository<Muqam> repository)
    {
        _repository = repository;
    }

    public async Task<Result<Guid>> Handle(CreateMuqamCommand request, CancellationToken cancellationToken)
    {
        var muqam = new Muqam(
            request.Request.Name,
            request.Request.Code,
            request.Request.DilaId
        );

        muqam.Update(
            request.Request.Name,
            request.Request.Code,
            request.Request.Address,
            request.Request.ContactPerson,
            request.Request.PhoneNumber,
            request.Request.Email
        );

        await _repository.AddAsync(muqam, cancellationToken);

        return Result<Guid>.Success(muqam.Id, "Muqam created successfully");
    }
}
