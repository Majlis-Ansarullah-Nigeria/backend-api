using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Application.Features.Identity.Invitations;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Organization.BulkImport;

/// <summary>
/// Re-parses and re-validates the file against current database state (never trusts a
/// client-held preview), then imports every non-Error row. Idempotent: re-running the same
/// file is a no-op for untouched rows (EF only emits an UPDATE when a value actually
/// changed) and never reassigns an existing active head (bulk-import plan's answered question).
/// </summary>
public sealed record CommitBulkImportCommand(Guid ActorUserId, byte[] FileContent, string FileName)
    : IRequest<Result<BulkImportCommitResult>>;

public sealed record BulkImportCommitResult(int CreatedCount, int UpdatedCount, int SkippedErrorCount, int InvitedCount);

public sealed class CommitBulkImportCommandHandler(
    IAppDbContext db,
    IAccessPolicy accessPolicy,
    IBulkImportFileReader fileReader,
    IIdentityService identityService,
    ISender sender,
    IClock clock,
    IAuditLog auditLog) : IRequestHandler<CommitBulkImportCommand, Result<BulkImportCommitResult>>
{
    public async ValueTask<Result<BulkImportCommitResult>> Handle(CommitBulkImportCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.ActorUserId, cancellationToken))
        {
            return Result.Failure<BulkImportCommitResult>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage the organization."));
        }

        var rawRows = fileReader.ReadRows(request.FileContent, request.FileName);
        if (rawRows.IsFailure)
        {
            return Result.Failure<BulkImportCommitResult>(rawRows.Error);
        }

        var resolved = await new BulkImportProcessor(db, clock).ResolveAsync(rawRows.Value, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<BulkImportCommitResult>(resolved.Error);
        }

        var now = clock.UtcNow;
        var createdCount = 0;
        var updatedCount = 0;
        var invitedCount = 0;

        foreach (var row in resolved.Value)
        {
            if (row.Outcome == BulkImportOutcome.Error)
            {
                continue;
            }

            if (row.ExistingUnit is { } existing)
            {
                existing.Rename(row.Name);
                existing.UpdateContact(row.Address, row.ContactPhone, row.ContactEmail, existing.Latitude, existing.Longitude);
                updatedCount++;
            }
            else if (row.NewUnit is { } created)
            {
                db.Units.Add(created);
                createdCount++;
            }

            if (row.ShouldSetHead && row.ResolvedUnit is { } unit)
            {
                invitedCount += await AssignOrInviteHeadAsync(row, unit, request.ActorUserId, now, cancellationToken);
            }
        }

        var skippedErrorCount = resolved.Value.Count(r => r.Outcome == BulkImportOutcome.Error);
        auditLog.Add(new AuditEntry(
            AuditActions.UnitsImported, request.ActorUserId, "unit", null,
            $"Imported units: {createdCount} created, {updatedCount} updated, {skippedErrorCount} skipped, {invitedCount} invited",
            After: new { Created = createdCount, Updated = updatedCount, Skipped = skippedErrorCount, Invited = invitedCount }));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new BulkImportCommitResult(createdCount, updatedCount, skippedErrorCount, invitedCount));
    }

    private async Task<int> AssignOrInviteHeadAsync(
        ResolvedBulkImportRow row, Domain.Organization.Unit unit, Guid actorUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var headUserId = await identityService.FindUserIdByEmailAsync(row.HeadEmail, cancellationToken);

        if (headUserId is { } existingUserId)
        {
            db.RoleAssignments.Add(new RoleAssignment(
                Guid.CreateVersion7(), existingUserId, unit.Id, RoleName.UnitHead,
                delegateScope: null, startsAt: now, endsAt: null, appointedBy: actorUserId));
            return 0;
        }

        var headName = string.IsNullOrEmpty(row.HeadName) ? row.HeadEmail : row.HeadName;
        var invited = await sender.Send(new CreateInvitationCommand(row.HeadEmail, headName, unit.Id), cancellationToken);

        // A conflict here (USER_ALREADY_EXISTS) means the email was registered between resolve
        // and this call — a benign race; the unit itself still imports either way.
        return invited.IsSuccess ? 1 : 0;
    }
}
