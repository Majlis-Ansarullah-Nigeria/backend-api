using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Templates.ReportTypes;

public sealed record ArchiveReportTypeCommand(Guid UserId, Guid ReportTypeId) : IRequest<Result>;
