using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.History;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.History;

public sealed record GetExportDownloadUrlQuery(Guid UserId, Guid ExportId) : IRequest<Result<string>>;

public sealed class GetExportDownloadUrlQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IFileStorage fileStorage)
    : IRequestHandler<GetExportDownloadUrlQuery, Result<string>>
{
    public async ValueTask<Result<string>> Handle(GetExportDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var export = await db.BulkExports.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.ExportId, cancellationToken);
        if (export is null)
        {
            return Result.Failure<string>(Error.NotFound("EXPORT_NOT_FOUND", "That export doesn't exist."));
        }

        var isOwner = export.RequestedByUserId == request.UserId;
        if (!isOwner && !await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<string>(Error.Forbidden("FORBIDDEN", "You don't have access to that export."));
        }

        if (export.Status != BulkExportStatus.Ready || export.StorageKey is null)
        {
            return Result.Failure<string>(Error.Conflict("EXPORT_NOT_READY", "This export isn't ready yet."));
        }

        var download = fileStorage.CreatePresignedDownload(export.StorageKey, TimeSpan.FromMinutes(15));
        return Result.Success(download.Url);
    }
}
