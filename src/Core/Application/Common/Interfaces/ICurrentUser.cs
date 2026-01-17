namespace ManagementApi.Application.Common.Interfaces;

public interface ICurrentUser
{
    Guid? GetUserId();
    string? GetUserEmail();
    string? GetChandaNo();
    Guid? GetMuqamId();
    Guid? GetDilaId();
    Guid? GetZoneId();
    bool IsAuthenticated();
    bool IsInRole(string role);
}
