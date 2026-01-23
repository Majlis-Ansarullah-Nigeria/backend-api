namespace ManagementApi.Shared.Authorization;

public static class Roles
{
    // ===== SYSTEM ROLES =====
    /// <summary>
    /// Super Admin - Technical administrator with all permissions
    /// </summary>
    public const string SuperAdmin = "SuperAdmin";

    // ===== NATIONAL LEVEL =====
    /// <summary>
    /// National Admin - Full administrative access nationwide
    /// </summary>
    public const string NationalAdmin = "NationalAdmin";

    /// <summary>
    /// National Secretary - Administrative support for National Admin (view/export)
    /// </summary>
    public const string NationalSecretary = "NationalSecretary";

    // ===== ZONE LEVEL =====
    /// <summary>
    /// Zone Nazim - Regional leadership, oversees multiple Dilas
    /// </summary>
    public const string ZoneNazim = "ZoneNazim";

    /// <summary>
    /// Zone Secretary - Administrative support for Zone Nazim (view only)
    /// </summary>
    public const string ZoneSecretary = "ZoneSecretary";

    // ===== DILA LEVEL =====
    /// <summary>
    /// Nazim Ala - Dila Coordinator, coordinates multiple Muqams
    /// </summary>
    public const string NazimAla = "NazimAla";

    /// <summary>
    /// Dila Secretary - Administrative support for Nazim Ala (view only)
    /// </summary>
    public const string DilaSecretary = "DilaSecretary";

    // ===== MUQAM LEVEL =====
    /// <summary>
    /// Zaim Ala - Muqam Leader, manages local Muqam
    /// </summary>
    public const string ZaimAla = "ZaimAla";

    /// <summary>
    /// Muqam Secretary - Administrative support for Zaim Ala (view only)
    /// </summary>
    public const string MuqamSecretary = "MuqamSecretary";

    // ===== DEFAULT ROLE =====
    /// <summary>
    /// Member - Default role for registered users without organizational position
    /// </summary>
    public const string Member = "Member";

    // ===== LEGACY ROLES (kept for backward compatibility) =====
    [Obsolete("Use NazimAla instead")]
    public const string NazimAala = "Nazim A'ala";

    [Obsolete("Use ZaimAla instead")]
    public const string ZaimAala = "Zaim A'ala";

    [Obsolete("Use ZoneNazim instead")]
    public const string ZonalCoordinator = "Zonal Coordinator";

    public static List<string> GetDefaultRoles()
    {
        return new List<string>
        {
            SuperAdmin,
            NationalAdmin,
            NationalSecretary,
            ZoneNazim,
            ZoneSecretary,
            NazimAla,
            DilaSecretary,
            ZaimAla,
            MuqamSecretary,
            Member
        };
    }

    /// <summary>
    /// Gets the default role-permission mapping based on RBAC implementation plan
    /// This mapping aligns with the frontend role-permission matrix
    /// </summary>
    public static Dictionary<string, List<string>> GetDefaultRolePermissions()
    {
        return new Dictionary<string, List<string>>
        {
            // ===== MUQAM LEVEL =====
            {
                ZaimAla, new List<string>
                {
                    // Dashboard
                    Permissions.DashboardView,

                    // Members (own Muqam only)
                    Permissions.MembersView,
                    Permissions.MembersEdit,

                    // Reports
                    Permissions.ReportsView,
                    Permissions.ReportsSubmit
                }
            },
            {
                MuqamSecretary, new List<string>
                {
                    // Dashboard
                    Permissions.DashboardView,

                    // Members (view only)
                    Permissions.MembersView,

                    // Reports
                    Permissions.ReportsView,
                    Permissions.ReportsSubmit
                }
            },

            // ===== DEFAULT ROLE =====
            {
                Member, new List<string>
                {
                    // Dashboard
                    Permissions.DashboardView,

                    // Members (view only - own profile)
                    Permissions.MembersView
                }
            },

            // ===== DILA LEVEL =====
            {
                NazimAla, new List<string>
                {
                    // Dashboard
                    Permissions.DashboardView,

                    // Members (all Muqams in Dila)
                    Permissions.MembersView,
                    Permissions.MembersCreate,
                    Permissions.MembersEdit,
                    Permissions.MembersExport,

                    // Jamaats (view only)
                    Permissions.JamaatsView,

                    // Reports
                    Permissions.ReportsView,
                    Permissions.ReportsSubmit,
                    Permissions.ReportsViewAll,    // See all Muqam reports in their Dila
                    Permissions.ReportsApprove
                }
            },
            {
                DilaSecretary, new List<string>
                {
                    // Dashboard
                    Permissions.DashboardView,

                    // Members (view only)
                    Permissions.MembersView,

                    // Reports (view only)
                    Permissions.ReportsView,
                    Permissions.ReportsViewAll
                }
            },

            // ===== ZONE LEVEL =====
            {
                ZoneNazim, new List<string>
                {
                    // Dashboard
                    Permissions.DashboardView,

                    // Members (all Dilas in Zone)
                    Permissions.MembersView,
                    Permissions.MembersCreate,
                    Permissions.MembersEdit,
                    Permissions.MembersExport,

                    // Organizations (view, limited edit)
                    Permissions.OrganizationsView,

                    // Jamaats
                    Permissions.JamaatsView,
                    Permissions.JamaatsMap,
                    Permissions.JamaatsUnmap,

                    // Reports
                    Permissions.ReportsView,
                    Permissions.ReportsViewAll,
                    Permissions.ReportsApprove,

                    // Users (within Zone)
                    Permissions.UsersView,
                    Permissions.UsersCreate,
                    Permissions.UsersEdit
                }
            },
            {
                ZoneSecretary, new List<string>
                {
                    // Dashboard
                    Permissions.DashboardView,

                    // Members (view only)
                    Permissions.MembersView,

                    // Organizations (view only)
                    Permissions.OrganizationsView,

                    // Reports (view only)
                    Permissions.ReportsView,
                    Permissions.ReportsViewAll
                }
            },

            // ===== NATIONAL LEVEL =====
            {
                NationalAdmin, new List<string>
                {
                    // Dashboard
                    Permissions.DashboardView,

                    // Members (nationwide)
                    Permissions.MembersView,
                    Permissions.MembersCreate,
                    Permissions.MembersEdit,
                    Permissions.MembersDelete,
                    Permissions.MembersExport,

                    // Organizations (full control)
                    Permissions.OrganizationsView,
                    Permissions.OrganizationsManage,
                    Permissions.OrganizationsCreate,
                    Permissions.OrganizationsEdit,
                    Permissions.OrganizationsDelete,

                    // Jamaats (full control)
                    Permissions.JamaatsView,
                    Permissions.JamaatsMap,
                    Permissions.JamaatsUnmap,

                    // Reports (full control)
                    Permissions.ReportsView,
                    Permissions.ReportsViewAll,
                    Permissions.ReportsApprove,
                    Permissions.ReportsDelete,

                    // Report Templates (full control)
                    Permissions.ReportTemplatesView,
                    Permissions.ReportTemplatesManage,
                    Permissions.ReportTemplatesCreate,
                    Permissions.ReportTemplatesEdit,
                    Permissions.ReportTemplatesDelete,

                    // Submission Windows (full control)
                    Permissions.SubmissionWindowsView,
                    Permissions.SubmissionWindowsManage,
                    Permissions.SubmissionWindowsCreate,
                    Permissions.SubmissionWindowsEdit,
                    Permissions.SubmissionWindowsDelete,

                    // Users & Roles (full control)
                    Permissions.UsersView,
                    Permissions.UsersManage,
                    Permissions.UsersCreate,
                    Permissions.UsersEdit,
                    Permissions.UsersDelete,

                    Permissions.RolesView,
                    Permissions.RolesManage,
                    Permissions.RolesCreate,
                    Permissions.RolesEdit,
                    Permissions.RolesDelete,

                    // Settings (full control)
                    Permissions.SettingsView,
                    Permissions.SettingsManage,
                    Permissions.SettingsManageTheme
                }
            },
            {
                NationalSecretary, new List<string>
                {
                    // Dashboard
                    Permissions.DashboardView,

                    // Members (view & export)
                    Permissions.MembersView,
                    Permissions.MembersExport,

                    // Organizations (view only)
                    Permissions.OrganizationsView,

                    // Reports (view all)
                    Permissions.ReportsView,
                    Permissions.ReportsViewAll,

                    // Templates (view only)
                    Permissions.ReportTemplatesView,
                    Permissions.SubmissionWindowsView
                }
            },

            // ===== SUPER ADMIN =====
            // SuperAdmin gets all permissions (handled in PermissionAuthorizationHandler)
        };
    }
}
