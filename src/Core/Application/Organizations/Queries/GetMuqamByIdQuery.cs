using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Organizations.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Organizations.Queries;

public record GetMuqamByIdQuery(Guid Id) : IRequest<Result<MuqamDto>>;

public class GetMuqamByIdQueryHandler : IRequestHandler<GetMuqamByIdQuery, Result<MuqamDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMuqamByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<MuqamDto>> Handle(GetMuqamByIdQuery request, CancellationToken cancellationToken)
    {
        var muqam = await _context.Muqams
            .Include(m => m.Dila)
            .Include(m => m.Members)
            .Include(m => m.Jamaats)
            .Where(m => m.Id == request.Id)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (muqam == null)
        {
            return Result<MuqamDto>.Failure("Muqam not found");
        }

        return Result<MuqamDto>.Success(muqam);
    }
}
