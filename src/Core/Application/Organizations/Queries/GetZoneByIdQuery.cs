using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Organizations.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Organizations.Queries;

public record GetZoneByIdQuery(Guid Id) : IRequest<Result<ZoneDto>>;

public class GetZoneByIdQueryHandler : IRequestHandler<GetZoneByIdQuery, Result<ZoneDto>>
{
    private readonly IApplicationDbContext _context;

    public GetZoneByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ZoneDto>> Handle(GetZoneByIdQuery request, CancellationToken cancellationToken)
    {
        var zone = await _context.Zones
            .Include(z => z.Dilas)
                .ThenInclude(d => d.Muqams)
                    .ThenInclude(m => m.Members)
            .Where(z => z.Id == request.Id)
            .Select(z => new ZoneDto
            {
                Id = z.Id,
                Name = z.Name,
                Code = z.Code,
                Address = z.Address,
                ContactPerson = z.ContactPerson,
                PhoneNumber = z.PhoneNumber,
                Email = z.Email,
                DilaCount = z.Dilas.Count,
                TotalMuqams = z.Dilas.SelectMany(d => d.Muqams).Count(),
                TotalMembers = z.Dilas.SelectMany(d => d.Muqams).SelectMany(m => m.Members).Count(),
                CreatedAt = z.CreatedOn
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (zone == null)
        {
            return Result<ZoneDto>.Failure("Zone not found");
        }

        return Result<ZoneDto>.Success(zone);
    }
}
