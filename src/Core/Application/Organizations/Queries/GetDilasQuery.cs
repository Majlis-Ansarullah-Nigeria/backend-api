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
                // Count members directly assigned to muqams + members from jamaats mapped to muqams in this dila
                TotalMembers = d.Muqams.SelectMany(m => m.Members).Count() +
                    _context.Members.Count(mem =>
                        mem.JamaatId.HasValue &&
                        _context.Jamaats.Any(j =>
                            j.JamaatId == mem.JamaatId.Value &&
                            j.MuqamId.HasValue &&
                            d.Muqams.Any(m => m.Id == j.MuqamId.Value))),
                CreatedAt = d.CreatedOn
            })
            .ToListAsync(cancellationToken);

        return Result<List<DilaDto>>.Success(dilas);
    }
}
