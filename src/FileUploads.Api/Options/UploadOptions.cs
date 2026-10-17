namespace FileUploads.Api.Options;

public sealed class UploadOptions
{
    public const string SectionName = "Upload";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    public string[] AllowedExtensions { get; set; } = [];
}
