using ManagementApi.Domain.Identity;

namespace ManagementApi.Application.Common.Interfaces;

public interface ITokenService
{
    Task<string> GenerateTokenAsync(ApplicationUser user);
}
