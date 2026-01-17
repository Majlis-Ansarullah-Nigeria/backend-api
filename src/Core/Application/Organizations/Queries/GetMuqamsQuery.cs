using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Organizations.DTOs;
using ManagementApi.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Organizations.Queries;

public record GetMuqamsQuery : IRequest<Result<List<MuqamDto>>>;

public class GetMuqamsQueryHandler : IRequestHandler<GetMuqamsQuery, Result<List<MuqamDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetMuqamsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<MuqamDto>>> Handle(GetMuqamsQuery request, CancellationToken cancellationToken)
    {
        var muqams = await _context.Muqams
            .Include(m => m.Dila)
            .Include(m => m.Members)
            .Include(m => m.Jamaats)
            .Select(m => new MuqamDto
            {
                Id = m.Id,
                Name = m.Name,
                Code = m.Code,
                Address = m.Address,
                ContactPerson = m.ContactPerson,
                PhoneNumber = m.PhoneNumber,
                Email = m.Email,
                DilaId = m.DilaId,
                DilaName = m.Dila != null ? m.Dila.Name : null,
                MemberCount = m.Members.Count,
                JamaatCount = m.Jamaats.Count,
                CreatedAt = m.CreatedOn
            })
            .ToListAsync(cancellationToken);

        return Result<List<MuqamDto>>.Success(muqams);
    }
}
