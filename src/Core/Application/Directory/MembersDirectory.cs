using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Profile;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Directory;

// Members directory: any signed-in member can look up other members.
// Only public fields leave the server: name, photo, Muqam/Dila/Zone and positions.
// Never date of birth, phone, email, address or membership number.

public record DirectoryMemberDto(Guid Id, string FullName, string? PhotoUrl, string? MuqamName, string? DilaName, string? ZoneName, List<PositionDto> Positions);

public record DirectoryResultDto(List<DirectoryMemberDto> Items, int TotalCount, int PageNumber, int PageSize, string? MyMuqamName);

/// <param name="Search">Name (at least 3 characters) or a full membership number. Empty lists the caller's own Muqam.</param>
public record SearchDirectoryQuery(
    string? Search,
    Guid? ZoneId,
    Guid? DilaId,
    Guid? MuqamId,
    string? Position,
    int PageNumber = 1,
    int PageSize = 25) : IRequest<Result<DirectoryResultDto>>;

public class SearchDirectoryQueryHandler : IRequestHandler<SearchDirectoryQuery, Result<DirectoryResultDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SearchDirectoryQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<DirectoryResultDto>> Handle(SearchDirectoryQuery request, CancellationToken cancellationToken)
    {
        var search = request.Search?.Trim() ?? string.Empty;
        var position = request.Position?.Trim();
        var filtered = request.ZoneId.HasValue || request.DilaId.HasValue || request.MuqamId.HasValue || !string.IsNullOrEmpty(position);
        if (search.Length is > 0 and < 3)
        {
            return Result<DirectoryResultDto>.Failure("Type at least 3 letters, or a full membership number.");
        }

        var myMuqamId = _currentUser.MuqamId;
        var myMuqamName = myMuqamId is null
            ? null
            : await _context.Muqams.AsNoTracking().Where(m => m.Id == myMuqamId).Select(m => m.Name).FirstOrDefaultAsync(cancellationToken);

        var members = _context.Members.AsNoTracking();
        if (search.Length == 0 && !filtered)
        {
            if (myMuqamId is null)
            {
                return Result<DirectoryResultDto>.Success(new DirectoryResultDto(new(), 0, 1, request.PageSize, null));
            }

            members = members.Where(m => m.MuqamId == myMuqamId);
        }

        if (search.Length > 0)
        {
            var term = search.ToLower();
            members = members.Where(m => m.ChandaNo == search
                || m.Surname.ToLower().Contains(term)
                || (m.FirstName != null && m.FirstName.ToLower().Contains(term))
                || (m.MiddleName != null && m.MiddleName.ToLower().Contains(term))
                || ((m.FirstName ?? "") + " " + m.Surname).ToLower().Contains(term));
        }

        if (request.MuqamId is { } muqamId)
        {
            members = members.Where(m => m.MuqamId == muqamId);
        }
        else if (request.DilaId is { } dilaId)
        {
            members = members.Where(m => m.Muqam != null && m.Muqam.DilaId == dilaId);
        }
        else if (request.ZoneId is { } zoneId)
        {
            members = members.Where(m => m.Muqam != null && m.Muqam.Dila != null && m.Muqam.Dila.ZoneId == zoneId);
        }

        if (!string.IsNullOrEmpty(position))
        {
            var title = position.ToLower();
            members = members.Where(m => _context.MemberPositions.Any(p => p.MemberId == m.Id && p.IsActive && p.PositionTitle.ToLower().Contains(title)));
        }

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var total = await members.CountAsync(cancellationToken);
        var page = await members
            .OrderBy(m => m.Surname).ThenBy(m => m.FirstName)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.Title,
                m.FirstName,
                m.MiddleName,
                m.Surname,
                m.PhotoUrl,
                Muqam = m.Muqam != null ? m.Muqam.Name : null,
                Dila = m.Muqam != null && m.Muqam.Dila != null ? m.Muqam.Dila.Name : null,
                Zone = m.Muqam != null && m.Muqam.Dila != null && m.Muqam.Dila.Zone != null ? m.Muqam.Dila.Zone.Name : null,
            })
            .ToListAsync(cancellationToken);

        var positions = await MemberPositions.ForMembersAsync(_context, page.Select(m => m.Id).ToList(), cancellationToken);
        var items = page.Select(m => new DirectoryMemberDto(
            m.Id,
            string.Join(' ', new[] { m.Title, m.FirstName, m.MiddleName, m.Surname }.Where(s => !string.IsNullOrWhiteSpace(s))),
            m.PhotoUrl,
            m.Muqam,
            m.Dila,
            m.Zone,
            positions.GetValueOrDefault(m.Id) ?? new())).ToList();

        return Result<DirectoryResultDto>.Success(new DirectoryResultDto(items, total, pageNumber, pageSize, myMuqamName));
    }
}

public record DirectoryUnitDto(Guid Id, string Name, Guid? ZoneId, Guid? DilaId);

public record DirectoryUnitsDto(List<DirectoryUnitDto> Zones, List<DirectoryUnitDto> Dilas, List<DirectoryUnitDto> Muqams);

/// <summary>Zone, Dila and Muqam names for the directory filters. Names only.</summary>
public record GetDirectoryUnitsQuery : IRequest<Result<DirectoryUnitsDto>>;

public class GetDirectoryUnitsQueryHandler : IRequestHandler<GetDirectoryUnitsQuery, Result<DirectoryUnitsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDirectoryUnitsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<DirectoryUnitsDto>> Handle(GetDirectoryUnitsQuery request, CancellationToken cancellationToken)
    {
        var zones = await _context.Zones.AsNoTracking().OrderBy(z => z.Name)
            .Select(z => new DirectoryUnitDto(z.Id, z.Name, null, null)).ToListAsync(cancellationToken);
        var dilas = await _context.Dilas.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new DirectoryUnitDto(d.Id, d.Name, d.ZoneId, null)).ToListAsync(cancellationToken);
        var muqams = await _context.Muqams.AsNoTracking().OrderBy(m => m.Name)
            .Select(m => new DirectoryUnitDto(m.Id, m.Name, null, m.DilaId)).ToListAsync(cancellationToken);
        return Result<DirectoryUnitsDto>.Success(new DirectoryUnitsDto(zones, dilas, muqams));
    }
}
