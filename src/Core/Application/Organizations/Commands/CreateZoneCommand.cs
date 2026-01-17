using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Organizations.DTOs;
using ManagementApi.Domain.Entities;
using MediatR;

namespace ManagementApi.Application.Organizations.Commands;

public record CreateZoneCommand(CreateZoneRequest Request) : IRequest<Result<Guid>>;

public class CreateZoneCommandHandler : IRequestHandler<CreateZoneCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateZoneCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateZoneCommand request, CancellationToken cancellationToken)
    {
        var zone = new Zone(request.Request.Name, request.Request.Code);

        zone.Update(
            request.Request.Name,
            request.Request.Code,
            request.Request.Address,
            request.Request.ContactPerson,
            request.Request.PhoneNumber,
            request.Request.Email
        );

        _context.Zones.Add(zone);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(zone.Id, "Zone created successfully");
    }
}
