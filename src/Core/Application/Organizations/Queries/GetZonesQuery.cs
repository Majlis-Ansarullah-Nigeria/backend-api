using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Organizations.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Organizations.Queries;

public record GetZonesQuery : IRequest<Result<List<ZoneDto>>>;

public class GetZonesQueryHandler : IRequestHandler<GetZonesQuery, Result<List<ZoneDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetZonesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ZoneDto>>> Handle(GetZonesQuery request, CancellationToken cancellationToken)
    {
        var zones = await _context.Zones
            .Include(z => z.Dilas)
            .ThenInclude(d => d.Muqams)
            .ThenInclude(m => m.Members)
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
            .ToListAsync(cancellationToken);

        return Result<List<ZoneDto>>.Success(zones);
    }
}
