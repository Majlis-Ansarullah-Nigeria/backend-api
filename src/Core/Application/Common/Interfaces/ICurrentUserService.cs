using ManagementApi.Domain.Enums;

namespace ManagementApi.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid UserId { get; }
    string? UserName { get; }
    string? ChandaNo { get; }
    string? Email { get; }
    Guid? MuqamId { get; }
    Guid? DilaId { get; }
    Guid? ZoneId { get; }
    OrganizationLevel OrganizationLevel { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    bool HasPermission(string permission);
}
