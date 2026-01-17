namespace ManagementApi.Shared.Authorization;

public static class Roles
{
    // System role - has all permissions
    public const string Admin = "Admin";

    // Default role - assigned to all new users
    public const string Member = "Member";

    // Organizational roles
    public const string NationalSecretary = "National Secretary";
    public const string ZonalCoordinator = "Zonal Coordinator";
    public const string NazimAala = "Nazim A'ala"; // Dila Head
    public const string ZaimAala = "Zaim A'ala"; // Muqam Head

    // Departmental roles
    public const string TajneedSecretary = "Tajneed Secretary";
    public const string MaalSecretary = "Maal Secretary";
    public const string TalimSecretary = "Talim Secretary";
    public const string TarbiyyatSecretary = "Tarbiyyat Secretary";

    public static List<string> GetDefaultRoles()
    {
        return new List<string>
        {
            Admin,
            Member,
            NationalSecretary,
            ZonalCoordinator,
            NazimAala,
            ZaimAala,
            TajneedSecretary,
            MaalSecretary,
            TalimSecretary,
            TarbiyyatSecretary
        };
    }

    public static Dictionary<string, List<string>> GetDefaultRolePermissions()
    {
        return new Dictionary<string, List<string>>
        {
            {
                Member, new List<string>
                {
                    Permissions.MembersView,
                    Permissions.ReportsView,
                    Permissions.ReportsSubmit
                }
            },
            {
                ZaimAala, new List<string>
                {
                    Permissions.MembersView,
                    Permissions.MembersEdit,
                    Permissions.JamaatsView,
                    Permissions.JamaatsMap,
                    Permissions.ReportsView,
                    Permissions.ReportsSubmit,
                    Permissions.ReportsEdit
                }
            },
            {
                NazimAala, new List<string>
                {
                    Permissions.MembersView,
                    Permissions.MembersEdit,
                    Permissions.JamaatsView,
                    Permissions.JamaatsMap,
                    Permissions.ReportsView,
                    Permissions.ReportsSubmit,
                    Permissions.ReportsEdit,
                    Permissions.ReportsApprove
                }
            },
            {
                ZonalCoordinator, new List<string>
                {
                    Permissions.MembersView,
                    Permissions.MembersEdit,
                    Permissions.JamaatsView,
                    Permissions.ReportsView,
                    Permissions.ReportsSubmit,
                    Permissions.ReportsEdit,
                    Permissions.ReportsApprove,
                    Permissions.ReportsViewAnalytics
                }
            },
            {
                NationalSecretary, new List<string>
                {
                    Permissions.MembersView,
                    Permissions.MembersEdit,
                    Permissions.MembersExport,
                    Permissions.JamaatsView,
                    Permissions.JamaatsMap,
                    Permissions.JamaatsSync,
                    Permissions.OrganizationsView,
                    Permissions.OrganizationsCreate,
                    Permissions.OrganizationsEdit,
                    Permissions.ReportsView,
                    Permissions.ReportsSubmit,
                    Permissions.ReportsEdit,
                    Permissions.ReportsApprove,
                    Permissions.ReportsReject,
                    Permissions.ReportsViewAnalytics,
                    Permissions.ReportTemplatesView,
                    Permissions.ReportTemplatesCreate,
                    Permissions.ReportTemplatesEdit
                }
            }
        };
    }
}
