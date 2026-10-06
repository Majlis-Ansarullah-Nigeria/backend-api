using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Organization.BulkImport;

/// <summary>Dry run only — never writes to the database (FR-ORG-003).</summary>
public sealed record PreviewBulkImportQuery(Guid ActorUserId, byte[] FileContent, string FileName)
    : IRequest<Result<BulkImportSummary>>;

public sealed class PreviewBulkImportQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IBulkImportFileReader fileReader, IClock clock)
    : IRequestHandler<PreviewBulkImportQuery, Result<BulkImportSummary>>
{
    public async ValueTask<Result<BulkImportSummary>> Handle(PreviewBulkImportQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.ActorUserId, cancellationToken))
        {
            return Result.Failure<BulkImportSummary>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage the organization."));
        }

        var rawRows = fileReader.ReadRows(request.FileContent, request.FileName);
        if (rawRows.IsFailure)
        {
            return Result.Failure<BulkImportSummary>(rawRows.Error);
        }

        var resolved = await new BulkImportProcessor(db, clock).ResolveAsync(rawRows.Value, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<BulkImportSummary>(resolved.Error);
        }

        var rows = resolved.Value.Select(r => r.ToPreviewRow()).ToList();
        return Result.Success(BulkImportSummary.FromRows(rows));
    }
}
