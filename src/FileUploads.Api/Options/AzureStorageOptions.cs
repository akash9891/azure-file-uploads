namespace FileUploads.Api.Options;

public sealed class AzureStorageOptions
{
    public const string SectionName = "AzureStorage";

    public string AccountName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
}
