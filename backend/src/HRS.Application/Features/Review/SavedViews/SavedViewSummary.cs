using HRS.Domain.Review;

namespace HRS.Application.Features.Review.SavedViews;

public sealed record SavedViewSummary(Guid Id, string Name, InboxFilter Filter)
{
    public static SavedViewSummary FromEntity(SavedView view) => new(
        view.Id, view.Name, System.Text.Json.JsonSerializer.Deserialize<InboxFilter>(view.FilterJson, System.Text.Json.JsonSerializerOptions.Web)!);
}

/// <summary>The exact shape GetInboxQuery's filter parameters take — a saved view is just this,
/// named and stored (FR-REV-001).</summary>
public sealed record InboxFilter(
    Guid? ReportTypeId,
    Guid? WindowId,
    Domain.Organization.UnitLevelCode? UnitLevel,
    Guid? ZoneUnitId,
    Domain.Submissions.ReportInstanceStatus? Status,
    bool? IsLate,
    bool? IsFlagged,
    Guid? AssignedToUserId);
