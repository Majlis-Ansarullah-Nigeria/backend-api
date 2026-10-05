namespace HRS.Domain.Access;

/// <summary>The fixed role vocabulary (hrs-domain skill). Not user-extensible, so a C# enum rather than a table.</summary>
public enum RoleName
{
    UnitHead,
    Delegate,
    Secretariat,
    Leadership,
    DeptOfficer,
    SysAdmin,
}
