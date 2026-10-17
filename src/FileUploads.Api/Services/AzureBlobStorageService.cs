using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace FileUploads.Api.Services;

public sealed class AzureBlobStorageService(BlobContainerClient container) : IBlobStorageService
{
    public async Task<Uri> UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        }, cancellationToken);
        return blob.Uri;
    }

    public Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken) =>
        container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
}
