using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Templates.TemplateVersions;

public sealed record PublishTemplateVersionCommand(Guid UserId, Guid ReportTypeId, Guid VersionId)
    : IRequest<Result<TemplateVersionSummary>>;
