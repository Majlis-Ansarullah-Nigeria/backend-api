namespace ManagementApi.Application.Common.Interfaces;

/// <summary>
/// Interface for member synchronization background job
/// </summary>
public interface IMemberSyncJob
{
    /// <summary>
    /// Executes the member synchronization job
    /// </summary>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for jamaat synchronization background job
/// </summary>
public interface IJamaatSyncJob
{
    /// <summary>
    /// Executes the jamaat synchronization job
    /// </summary>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
