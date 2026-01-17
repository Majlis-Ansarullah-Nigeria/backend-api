using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Jamaats.DTOs;
using ManagementApi.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Jamaats.Commands;

public record MapJamaatToMuqamCommand(MapJamaatToMuqamRequest Request) : IRequest<Result>;

public class MapJamaatToMuqamCommandValidator : AbstractValidator<MapJamaatToMuqamCommand>
{
    public MapJamaatToMuqamCommandValidator()
    {
        RuleFor(x => x.Request.JamaatId)
            .NotEmpty().WithMessage("Jamaat ID is required");

        RuleFor(x => x.Request.MuqamId)
            .NotEmpty().WithMessage("Muqam ID is required");
    }
}

public class MapJamaatToMuqamCommandHandler : IRequestHandler<MapJamaatToMuqamCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public MapJamaatToMuqamCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(MapJamaatToMuqamCommand request, CancellationToken cancellationToken)
    {
        // Get the Jamaat
        var jamaat = await _context.Jamaats
            .FirstOrDefaultAsync(j => j.Id == request.Request.JamaatId, cancellationToken);

        if (jamaat == null)
        {
            return Result.Failure("Jamaat not found");
        }

        // Verify Muqam exists
        var muqamExists = await _context.Muqams
            .AnyAsync(m => m.Id == request.Request.MuqamId, cancellationToken);

        if (!muqamExists)
        {
            return Result.Failure("Muqam not found");
        }

        // Map the Jamaat to the Muqam (this will auto-remove from previous Muqam if needed)
        jamaat.MapToMuqam(request.Request.MuqamId);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success("Jamaat successfully mapped to Muqam");
    }
}
