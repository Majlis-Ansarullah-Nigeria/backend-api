using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Organization.BulkImport;

public sealed record GenerateBulkImportTemplateQuery(Guid ActorUserId) : IRequest<Result<byte[]>>;

public sealed class GenerateBulkImportTemplateQueryHandler(IAccessPolicy accessPolicy, IBulkImportTemplateGenerator generator)
    : IRequestHandler<GenerateBulkImportTemplateQuery, Result<byte[]>>
{
    public async ValueTask<Result<byte[]>> Handle(GenerateBulkImportTemplateQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.ActorUserId, cancellationToken))
        {
            return Result.Failure<byte[]>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage the organization."));
        }

        return Result.Success(generator.Generate());
    }
}
