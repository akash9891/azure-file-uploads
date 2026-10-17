namespace FileUploads.Api.Services;

public interface IBlobStorageService
{
    /// <summary>Uploads the stream as a new blob and returns the blob's URL.</summary>
    Task<Uri> UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken);

    Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken);
}
