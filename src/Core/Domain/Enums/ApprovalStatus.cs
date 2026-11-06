namespace ManagementApi.Domain.Enums;

/// <summary>
/// A review action taken by the National Secretariat on a report, kept as history.
/// </summary>
public enum ApprovalStatus
{
    Pending = 1,
    Acknowledged = 2,
    Returned = 3,
    Reopened = 4
}
