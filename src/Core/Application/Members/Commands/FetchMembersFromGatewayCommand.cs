using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Domain.Entities;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ManagementApi.Application.Members.Commands;

public record FetchMembersFromGatewayCommand : IRequest<Result<MemberSyncResult>>;

public record MemberSyncResult
{
    public int TotalFetched { get; init; }
    public int NewMembers { get; init; }
    public int UpdatedMembers { get; init; }
    public int FailedMembers { get; init; }
    public List<string> Errors { get; init; } = new();
}

public class FetchMembersFromGatewayCommandHandler : IRequestHandler<FetchMembersFromGatewayCommand, Result<MemberSyncResult>>
{
    private readonly IExternalMembersService _externalMembersService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<FetchMembersFromGatewayCommandHandler> _logger;

    public FetchMembersFromGatewayCommandHandler(
        IExternalMembersService externalMembersService,
        IApplicationDbContext context,
        ILogger<FetchMembersFromGatewayCommandHandler> logger)
    {
        _externalMembersService = externalMembersService;
        _context = context;
        _logger = logger;
    }

    public async Task<Result<MemberSyncResult>> Handle(FetchMembersFromGatewayCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting member sync from external gateway");

            // Fetch members from external gateway
            var externalMembers = await _externalMembersService.FetchMembersAsync(cancellationToken);
            _logger.LogInformation("Fetched {Count} members from external gateway", externalMembers.Count);

            int newMembers = 0;
            int updatedMembers = 0;
            int failedMembers = 0;
            var errors = new List<string>();

            foreach (var externalMember in externalMembers)
            {
                try
                {
                    // Check if member already exists
                    var existingMember = await _context.Members
                        .FirstOrDefaultAsync(m => m.ChandaNo == externalMember.ChandaNo, cancellationToken);

                    if (existingMember == null)
                    {
                        // Create new member
                        var newMember = new Member(
                            externalMember.ChandaNo,
                            externalMember.Surname,
                            externalMember.FirstName
                        );

                        // Update additional details
                        newMember.UpdateBasicInfo(
                            externalMember.Surname,
                            externalMember.FirstName,
                            externalMember.MiddleName,
                            externalMember.DateOfBirth,
                            externalMember.Email,
                            externalMember.PhoneNo,
                            externalMember.MaritalStatus,
                            externalMember.Address
                        );

                        if (!string.IsNullOrEmpty(externalMember.PhotoUrl))
                        {
                            newMember.UpdatePhotoUrl(externalMember.PhotoUrl);
                        }

                        if (!string.IsNullOrEmpty(externalMember.Signature))
                        {
                            newMember.UpdateSignature(externalMember.Signature);
                        }

                        if (externalMember.JamaatId.HasValue)
                        {
                            newMember.UpdateJamaatId(externalMember.JamaatId.Value);
                        }

                        // Parse blood group and genotype
                        if (Enum.TryParse<BloodGroup>(externalMember.BloodGroup, out var bloodGroup) &&
                            Enum.TryParse<Genotype>(externalMember.Genotype, out var genotype))
                        {
                            newMember.UpdateMedicalInfo(bloodGroup, genotype);
                        }

                        _context.Members.Add(newMember);
                        newMembers++;
                    }
                    else
                    {
                        // Update existing member
                        existingMember.UpdateBasicInfo(
                            externalMember.Surname,
                            externalMember.FirstName,
                            externalMember.MiddleName,
                            externalMember.DateOfBirth,
                            externalMember.Email,
                            externalMember.PhoneNo,
                            externalMember.MaritalStatus,
                            externalMember.Address
                        );

                        if (!string.IsNullOrEmpty(externalMember.PhotoUrl))
                        {
                            existingMember.UpdatePhotoUrl(externalMember.PhotoUrl);
                        }

                        if (!string.IsNullOrEmpty(externalMember.Signature))
                        {
                            existingMember.UpdateSignature(externalMember.Signature);
                        }

                        if (externalMember.JamaatId.HasValue)
                        {
                            existingMember.UpdateJamaatId(externalMember.JamaatId.Value);
                        }

                        if (Enum.TryParse<BloodGroup>(externalMember.BloodGroup, out var bloodGroup) &&
                            Enum.TryParse<Genotype>(externalMember.Genotype, out var genotype))
                        {
                            existingMember.UpdateMedicalInfo(bloodGroup, genotype);
                        }

                        updatedMembers++;
                    }
                }
                catch (Exception ex)
                {
                    failedMembers++;
                    var error = $"Failed to sync member {externalMember.ChandaNo}: {ex.Message}";
                    errors.Add(error);
                    _logger.LogError(ex, error);
                }
            }

            // Save all changes
            await _context.SaveChangesAsync(cancellationToken);

            var result = new MemberSyncResult
            {
                TotalFetched = externalMembers.Count,
                NewMembers = newMembers,
                UpdatedMembers = updatedMembers,
                FailedMembers = failedMembers,
                Errors = errors
            };

            _logger.LogInformation(
                "Member sync completed: {Total} fetched, {New} new, {Updated} updated, {Failed} failed",
                result.TotalFetched, result.NewMembers, result.UpdatedMembers, result.FailedMembers);

            return Result<MemberSyncResult>.Success(result, "Member sync completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during member sync");
            return Result<MemberSyncResult>.Failure($"Member sync failed: {ex.Message}");
        }
    }
}
