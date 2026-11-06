using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using Mediator;

namespace HRS.Application.Features.Templates.ReportTypes;

public sealed record UpdateReportTypeCommand(
    Guid UserId,
    Guid ReportTypeId,
    string Name,
    string? Description,
    ReportTypeCategory Category,
    ReportTypeFrequency Frequency,
    IReadOnlyList<UnitLevelCode> ApplicableLevels,
    UnitLevelCode RecipientLevel,
    int ObserverDepth,
    bool AllowAttachments,
    bool AllowNilReport,
    string? Code = null) : IRequest<Result<ReportTypeSummary>>;
