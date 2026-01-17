using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Organizations.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Organizations.Queries;

public record GetDilasQuery : IRequest<Result<List<DilaDto>>>;

public class GetDilasQueryHandler : IRequestHandler<GetDilasQuery, Result<List<DilaDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetDilasQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<DilaDto>>> Handle(GetDilasQuery request, CancellationToken cancellationToken)
    {
        var dilas = await _context.Dilas
            .Include(d => d.Zone)
            .Include(d => d.Muqams)
            .ThenInclude(m => m.Members)
            .Select(d => new DilaDto
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                Address = d.Address,
                ContactPerson = d.ContactPerson,
                PhoneNumber = d.PhoneNumber,
                Email = d.Email,
                ZoneId = d.ZoneId,
                ZoneName = d.Zone != null ? d.Zone.Name : null,
                MuqamCount = d.Muqams.Count,
                TotalMembers = d.Muqams.SelectMany(m => m.Members).Count(),
                CreatedAt = d.CreatedOn
            })
            .ToListAsync(cancellationToken);

        return Result<List<DilaDto>>.Success(dilas);
    }
}
