using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Organizations.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Organizations.Commands;

public record UpdateMuqamCommand(UpdateMuqamRequest Request) : IRequest<Result<Guid>>;

public class UpdateMuqamCommandHandler : IRequestHandler<UpdateMuqamCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public UpdateMuqamCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(UpdateMuqamCommand request, CancellationToken cancellationToken)
    {
        var muqam = await _context.Muqams.FindAsync(new object[] { request.Request.Id }, cancellationToken);

        if (muqam == null)
        {
            return Result<Guid>.Failure("Muqam not found");
        }

        muqam.Update(
            request.Request.Name,
            request.Request.Code,
            request.Request.Address,
            request.Request.ContactPerson,
            request.Request.PhoneNumber,
            request.Request.Email
        );

        if (request.Request.DilaId.HasValue)
        {
            muqam.AssignToDila(request.Request.DilaId.Value);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(muqam.Id, "Muqam updated successfully");
    }
}
