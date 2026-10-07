using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Templates.TemplateVersions;

/// <summary>
/// The single entry point for editing a template (FR-TPL-008). The handler decides what
/// "editing" means for the current state: if a Draft is already open it's updated in place; if
/// the latest version is Published (immutable), a new Draft is forked instead — the caller
/// never names a version explicitly.
/// </summary>
public sealed record UpdateTemplateSchemaCommand(Guid UserId, Guid ReportTypeId, TemplateSchema Schema)
    : IRequest<Result<TemplateVersionSummary>>;
