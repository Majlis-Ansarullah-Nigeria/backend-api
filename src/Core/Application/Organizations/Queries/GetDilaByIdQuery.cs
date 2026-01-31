using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Organizations.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Organizations.Queries;

public record GetDilaByIdQuery(Guid Id) : IRequest<Result<DilaDto>>;

public class GetDilaByIdQueryHandler : IRequestHandler<GetDilaByIdQuery, Result<DilaDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDilaByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DilaDto>> Handle(GetDilaByIdQuery request, CancellationToken cancellationToken)
    {
        var dila = await _context.Dilas
            .Include(d => d.Zone)
            .Where(d => d.Id == request.Id)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (dila == null)
        {
            return Result<DilaDto>.Failure("Dila not found");
        }

        return Result<DilaDto>.Success(dila);
    }
}
