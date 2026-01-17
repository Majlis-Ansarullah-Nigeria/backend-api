using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Users.DTOs;
using ManagementApi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Users.Queries;

public record GetUsersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null
) : IRequest<Result<PaginationResponse<UserWithRolesDto>>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, Result<PaginationResponse<UserWithRolesDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetUsersQueryHandler(
        IApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<Result<PaginationResponse<UserWithRolesDto>>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Users.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(u =>
                (u.ChandaNo != null && u.ChandaNo.ToLower().Contains(searchTerm)) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(searchTerm)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(searchTerm)) ||
                (u.Email != null && u.Email.ToLower().Contains(searchTerm))
            );
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var users = await query
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Map to DTOs with roles
        var userDtos = new List<UserWithRolesDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);
            var permissions = claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToList();

            userDtos.Add(new UserWithRolesDto
            {
                Id = user.Id,
                ChandaNo = user.ChandaNo,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                IsActive = user.IsActive,
                Roles = roles.ToList(),
                Permissions = permissions
            });
        }

        var response = PaginationResponse<UserWithRolesDto>.Success(
            userDtos,
            totalCount,
            request.PageNumber,
            request.PageSize
        );

        return Result<PaginationResponse<UserWithRolesDto>>.Success(response);
    }
}
