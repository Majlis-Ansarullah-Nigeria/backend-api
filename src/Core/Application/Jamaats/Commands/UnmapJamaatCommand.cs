using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Jamaats.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Jamaats.Commands;

public record UnmapJamaatCommand(UnmapJamaatRequest Request) : IRequest<Result>;

public class UnmapJamaatCommandValidator : AbstractValidator<UnmapJamaatCommand>
{
    public UnmapJamaatCommandValidator()
    {
        RuleFor(x => x.Request.JamaatId)
            .NotEmpty().WithMessage("Jamaat ID is required");
    }
}

public class UnmapJamaatCommandHandler : IRequestHandler<UnmapJamaatCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UnmapJamaatCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UnmapJamaatCommand request, CancellationToken cancellationToken)
    {
        var jamaat = await _context.Jamaats
            .FirstOrDefaultAsync(j => j.Id == request.Request.JamaatId, cancellationToken);

        if (jamaat == null)
        {
            return Result.Failure("Jamaat not found");
        }

        if (!jamaat.MuqamId.HasValue)
        {
            return Result.Failure("Jamaat is not mapped to any Muqam");
        }

        jamaat.Unmap();

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success("Jamaat successfully unmapped");
    }
}
