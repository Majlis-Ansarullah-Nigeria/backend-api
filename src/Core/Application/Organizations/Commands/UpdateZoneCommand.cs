using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Organizations.DTOs;
using MediatR;

namespace ManagementApi.Application.Organizations.Commands;

public record UpdateZoneCommand(UpdateZoneRequest Request) : IRequest<Result<Guid>>;

public class UpdateZoneCommandHandler : IRequestHandler<UpdateZoneCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public UpdateZoneCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(UpdateZoneCommand request, CancellationToken cancellationToken)
    {
        var zone = await _context.Zones.FindAsync(new object[] { request.Request.Id }, cancellationToken);

        if (zone == null)
        {
            return Result<Guid>.Failure("Zone not found");
        }

        zone.Update(
            request.Request.Name,
            request.Request.Code,
            request.Request.Address,
            request.Request.ContactPerson,
            request.Request.PhoneNumber,
            request.Request.Email
        );

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(zone.Id, "Zone updated successfully");
    }
}
