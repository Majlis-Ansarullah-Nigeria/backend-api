using Microsoft.AspNetCore.Authorization;

namespace ManagementApi.Infrastructure.Authorization;

public class MustHavePermissionAttribute : AuthorizeAttribute
{
    public MustHavePermissionAttribute(string permission)
    {
        Policy = permission;
    }
}
