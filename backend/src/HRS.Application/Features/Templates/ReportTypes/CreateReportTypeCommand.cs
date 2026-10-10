using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using Mediator;

namespace HRS.Application.Features.Templates.ReportTypes;

public sealed record CreateReportTypeCommand(
    Guid UserId,
    string Name,
    string? Description,
    ReportTypeCategory Category,
    ReportTypeFrequency Frequency,
    IReadOnlyList<UnitLevelCode> ApplicableLevels,
    UnitLevelCode RecipientLevel,
    int ObserverDepth,
    bool AllowAttachments,
    bool AllowNilReport) : IRequest<Result<ReportTypeSummary>>;
