using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ManagementApi.Application.Jamaats.Commands;

public record FetchJamaatsFromApiCommand : IRequest<Result<JamaatSyncResult>>;

public record JamaatSyncResult
{
    public int TotalFetched { get; init; }
    public int NewJamaats { get; init; }
    public int UpdatedJamaats { get; init; }
    public int FailedJamaats { get; init; }
    public List<string> Errors { get; init; } = new();
}

public class FetchJamaatsFromApiCommandHandler : IRequestHandler<FetchJamaatsFromApiCommand, Result<JamaatSyncResult>>
{
    private readonly IExternalJamaatsService _externalJamaatsService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<FetchJamaatsFromApiCommandHandler> _logger;

    public FetchJamaatsFromApiCommandHandler(
        IExternalJamaatsService externalJamaatsService,
        IApplicationDbContext context,
        ILogger<FetchJamaatsFromApiCommandHandler> logger)
    {
        _externalJamaatsService = externalJamaatsService;
        _context = context;
        _logger = logger;
    }

    public async Task<Result<JamaatSyncResult>> Handle(FetchJamaatsFromApiCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting jamaat sync from external API");

            // Fetch jamaats from external API
            var externalJamaats = await _externalJamaatsService.FetchJamaatsAsync(cancellationToken);
            _logger.LogInformation("Fetched {Count} jamaats from external API", externalJamaats.Count);

            int newJamaats = 0;
            int updatedJamaats = 0;
            int failedJamaats = 0;
            var errors = new List<string>();

            foreach (var externalJamaat in externalJamaats)
            {
                try
                {
                    // Check if jamaat already exists
                    var existingJamaat = await _context.Jamaats
                        .FirstOrDefaultAsync(j => j.JamaatId == externalJamaat.JamaatId, cancellationToken);

                    if (existingJamaat == null)
                    {
                        // Create new jamaat with circuit name
                        var newJamaat = new Jamaat(
                            externalJamaat.JamaatId,
                            externalJamaat.JamaatName,
                            externalJamaat.JamaatCode,
                            externalJamaat.CircuitName
                        );

                        _context.Jamaats.Add(newJamaat);
                        newJamaats++;
                    }
                    else
                    {
                        // Update existing jamaat with circuit name
                        existingJamaat.UpdateInfo(
                            externalJamaat.JamaatName,
                            externalJamaat.JamaatCode,
                            externalJamaat.CircuitName
                        );

                        updatedJamaats++;
                    }
                }
                catch (Exception ex)
                {
                    failedJamaats++;
                    var error = $"Failed to sync jamaat {externalJamaat.JamaatId} ({externalJamaat.Name}): {ex.Message}";
                    errors.Add(error);
                    _logger.LogError(ex, error);
                }
            }

            // Save all changes
            await _context.SaveChangesAsync(cancellationToken);

            var result = new JamaatSyncResult
            {
                TotalFetched = externalJamaats.Count,
                NewJamaats = newJamaats,
                UpdatedJamaats = updatedJamaats,
                FailedJamaats = failedJamaats,
                Errors = errors
            };

            _logger.LogInformation(
                "Jamaat sync completed: {Total} fetched, {New} new, {Updated} updated, {Failed} failed",
                result.TotalFetched, result.NewJamaats, result.UpdatedJamaats, result.FailedJamaats);

            return Result<JamaatSyncResult>.Success(result, "Jamaat sync completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during jamaat sync");
            return Result<JamaatSyncResult>.Failure($"Jamaat sync failed: {ex.Message}");
        }
    }
}
