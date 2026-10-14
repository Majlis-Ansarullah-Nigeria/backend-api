using System.Collections.Concurrent;
using HRS.Application.Common.Interfaces;

namespace HRS.Api.IntegrationTests;

/// <summary>
/// Replaces the real Hangfire-backed <see cref="IBackgroundJobScheduler"/> in tests — the test
/// host never calls AddHangfireServer() (only the Worker does), so a real enqueue would just sit
/// inertly in the test database forever. Tests instead call BulkExportProcessor.RunAsync
/// directly, the same way OutboxDispatcherTests/WindowJobRunnerTests call their own processors
/// directly rather than relying on a real background scheduler.
/// </summary>
public sealed class FakeBackgroundJobScheduler : IBackgroundJobScheduler
{
    private readonly ConcurrentBag<Guid> _enqueuedBulkExportIds = [];

    public void EnqueueBulkExport(Guid bulkExportId) => _enqueuedBulkExportIds.Add(bulkExportId);

    public bool WasEnqueued(Guid bulkExportId) => _enqueuedBulkExportIds.Contains(bulkExportId);
}
