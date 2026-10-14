namespace HRS.Application.Common.Interfaces;

/// <summary>FR-HIS-004's "large exports run in the background" — the Hangfire-backed real
/// implementation lives in Infrastructure (same reasoning as IEmailSender/IFileStorage: a
/// background-job framework is an infrastructure concern, not something Application code should
/// reference directly).</summary>
public interface IBackgroundJobScheduler
{
    void EnqueueBulkExport(Guid bulkExportId);
}
