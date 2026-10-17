using System.Collections.Concurrent;
using FileUploads.Api.Services;

namespace FileUploads.Api.Tests;

public sealed class FakeBlobStorageService : IBlobStorageService
{
    public ConcurrentDictionary<string, byte[]> Blobs { get; } = new();
    public ConcurrentBag<string> Deleted { get; } = [];

    public async Task<Uri> UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Blobs[blobName] = buffer.ToArray();
        return new Uri($"https://fake.blob.core.windows.net/uploads/{blobName}");
    }

    public Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken)
    {
        Blobs.TryRemove(blobName, out _);
        Deleted.Add(blobName);
        return Task.CompletedTask;
    }
}
