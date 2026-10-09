using System.Collections.Concurrent;
using HRS.Application.Common.Interfaces;

namespace HRS.Api.IntegrationTests;

/// <summary>
/// Replaces the real S3-backed <see cref="IFileStorage"/> in tests. The real flow has the
/// browser PUT bytes directly to a presigned URL, bypassing the API entirely — there's nothing
/// for a test's HttpClient to PUT to, so tests call <see cref="Seed"/> directly on this fake to
/// simulate "the client already uploaded the bytes to that key" instead of actually performing
/// an HTTP PUT.
/// </summary>
public sealed class FakeFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new();

    public void Seed(string key, byte[] content) => _objects[key] = content;

    public bool Contains(string key) => _objects.ContainsKey(key);

    public PresignedUpload CreatePresignedUpload(string key, TimeSpan expiresIn) =>
        new($"https://fake-storage.test/{key}", DateTimeOffset.UtcNow.Add(expiresIn));

    public Task<StoredFile?> GetAsync(string key, CancellationToken cancellationToken)
    {
        if (!_objects.TryGetValue(key, out var content))
        {
            return Task.FromResult<StoredFile?>(null);
        }

        return Task.FromResult<StoredFile?>(new StoredFile(new MemoryStream(content), content.Length));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
