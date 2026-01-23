namespace ManagementApi.Shared.Authorization;

public static class Permissions
{
    // Dashboard
    public const string DashboardView = "dashboard.view";

    // Members
    public const string MembersView = "members.view";
    public const string MembersCreate = "members.create";
    public const string MembersEdit = "members.edit";
    public const string MembersDelete = "members.delete";
    public const string MembersExport = "members.export";

    // Jamaats
    public const string JamaatsView = "jamaats.view";
    public const string JamaatsMap = "jamaats.map";
    public const string JamaatsUnmap = "jamaats.unmap";
    public const string JamaatsSync = "jamaats.sync";

    // Organizations
    public const string OrganizationsView = "organizations.view";
    public const string OrganizationsManage = "organizations.manage";
    public const string OrganizationsCreate = "organizations.create";
    public const string OrganizationsEdit = "organizations.edit";
    public const string OrganizationsDelete = "organizations.delete";

    // Reports
    public const string ReportsView = "reports.view";
    public const string ReportsSubmit = "reports.submit";
    public const string ReportsEdit = "reports.edit";
    public const string ReportsDelete = "reports.delete";
    public const string ReportsApprove = "reports.approve";
    public const string ReportsReject = "reports.reject";
    public const string ReportsViewAll = "reports.viewall";
    public const string ReportsViewAnalytics = "reports.viewanalytics";

    // Report Templates
    public const string ReportTemplatesView = "reporttemplates.view";
    public const string ReportTemplatesManage = "reporttemplates.manage";
    public const string ReportTemplatesCreate = "reporttemplates.create";
    public const string ReportTemplatesEdit = "reporttemplates.edit";
    public const string ReportTemplatesDelete = "reporttemplates.delete";

    // Submission Windows
    public const string SubmissionWindowsView = "submissionwindows.view";
    public const string SubmissionWindowsManage = "submissionwindows.manage";
    public const string SubmissionWindowsCreate = "submissionwindows.create";
    public const string SubmissionWindowsEdit = "submissionwindows.edit";
    public const string SubmissionWindowsDelete = "submissionwindows.delete";

    // Users & Roles
    public const string UsersView = "users.view";
    public const string UsersManage = "users.manage";
    public const string UsersCreate = "users.create";
    public const string UsersEdit = "users.edit";
    public const string UsersDelete = "users.delete";
    public const string UsersAssignRoles = "users.assignroles";

    public const string RolesView = "roles.view";
    public const string RolesManage = "roles.manage";
    public const string RolesCreate = "roles.create";
    public const string RolesEdit = "roles.edit";
    public const string RolesDelete = "roles.delete";
    public const string RolesManagePermissions = "roles.managepermissions";

    // Settings
    public const string SettingsView = "settings.view";
    public const string SettingsManage = "settings.manage";
    public const string SettingsManageTheme = "settings.managetheme";

    public static List<string> GetAllPermissions()
    {
        return typeof(Permissions)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => f.GetValue(null)?.ToString() ?? string.Empty)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();
    }
}
