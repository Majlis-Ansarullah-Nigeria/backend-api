namespace ManagementApi.Shared.Authorization;

public static class Permissions
{
    // Members
    public const string MembersView = "Members.View";
    public const string MembersCreate = "Members.Create";
    public const string MembersEdit = "Members.Edit";
    public const string MembersDelete = "Members.Delete";
    public const string MembersExport = "Members.Export";

    // Jamaats
    public const string JamaatsView = "Jamaats.View";
    public const string JamaatsMap = "Jamaats.Map";
    public const string JamaatsUnmap = "Jamaats.Unmap";
    public const string JamaatsSync = "Jamaats.Sync";

    // Organizations
    public const string OrganizationsView = "Organizations.View";
    public const string OrganizationsCreate = "Organizations.Create";
    public const string OrganizationsEdit = "Organizations.Edit";
    public const string OrganizationsDelete = "Organizations.Delete";

    // Reports
    public const string ReportsView = "Reports.View";
    public const string ReportsSubmit = "Reports.Submit";
    public const string ReportsEdit = "Reports.Edit";
    public const string ReportsDelete = "Reports.Delete";
    public const string ReportsApprove = "Reports.Approve";
    public const string ReportsReject = "Reports.Reject";
    public const string ReportsViewAnalytics = "Reports.ViewAnalytics";

    // Report Templates
    public const string ReportTemplatesView = "ReportTemplates.View";
    public const string ReportTemplatesCreate = "ReportTemplates.Create";
    public const string ReportTemplatesEdit = "ReportTemplates.Edit";
    public const string ReportTemplatesDelete = "ReportTemplates.Delete";

    // Users & Roles
    public const string UsersView = "Users.View";
    public const string UsersCreate = "Users.Create";
    public const string UsersEdit = "Users.Edit";
    public const string UsersDelete = "Users.Delete";
    public const string UsersAssignRoles = "Users.AssignRoles";
    public const string RolesView = "Roles.View";
    public const string RolesCreate = "Roles.Create";
    public const string RolesEdit = "Roles.Edit";
    public const string RolesDelete = "Roles.Delete";
    public const string RolesManagePermissions = "Roles.ManagePermissions";

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
