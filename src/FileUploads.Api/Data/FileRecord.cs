namespace FileUploads.Api.Data;

public sealed class FileRecord
{
    public Guid Id { get; set; }
    public required string OriginalFileName { get; set; }
    public required string BlobName { get; set; }
    public long SizeBytes { get; set; }
    public required string ContentType { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public required string BlobUrl { get; set; }
}
