namespace ManagementApi.Domain.Enums;

/// <summary>
/// Lifecycle of a unit's report in a submission window.
/// "Not started" is not stored: it is a unit with no report row in an open window.
/// </summary>
public enum SubmissionStatus
{
    Draft = 1,
    Submitted = 2,
    InReview = 3,
    Acknowledged = 4,
    Returned = 5,
    Missed = 6
}
