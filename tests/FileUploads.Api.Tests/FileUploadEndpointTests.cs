using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FileUploads.Api.Data;
using Microsoft.Extensions.DependencyInjection;

namespace FileUploads.Api.Tests;

public sealed class FileUploadEndpointTests : IDisposable
{
    private readonly FileUploadsApiFactory _factory = new();
    private readonly HttpClient _client;

    public FileUploadEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static MultipartFormDataContent FileForm(string fileName, byte[] content, string contentType = "text/plain")
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    [Fact]
    public async Task Upload_ValidFile_Returns201AndPersistsRecord()
    {
        var content = Encoding.UTF8.GetBytes("hello azure");

        var response = await _client.PostAsync("/api/files", FileForm("notes.txt", content));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var record = await response.Content.ReadFromJsonAsync<FileRecord>();
        Assert.NotNull(record);
        Assert.Equal("notes.txt", record.OriginalFileName);
        Assert.Equal(content.Length, record.SizeBytes);
        Assert.Equal("text/plain", record.ContentType);
        Assert.EndsWith(".txt", record.BlobName);
        Assert.Equal($"https://fake.blob.core.windows.net/uploads/{record.BlobName}", record.BlobUrl);
        Assert.Equal($"/api/files/{record.Id}", response.Headers.Location?.AbsolutePath);

        Assert.Equal(content, _factory.BlobStorage.Blobs[record.BlobName]);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.FileRecords.FindAsync(record.Id);
        Assert.NotNull(saved);
        Assert.Equal(record.BlobUrl, saved.BlobUrl);
    }

    [Fact]
    public async Task Upload_ClientSuppliedPath_IsNotUsedForBlobOrFileName()
    {
        var response = await _client.PostAsync("/api/files", FileForm("../../etc/evil.TXT", [1, 2, 3]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var record = await response.Content.ReadFromJsonAsync<FileRecord>();
        Assert.NotNull(record);
        Assert.Equal("evil.TXT", record.OriginalFileName);
        Assert.Matches("^[0-9a-f]{32}\\.txt$", record.BlobName);
    }

    [Fact]
    public async Task Upload_NoFile_Returns400()
    {
        var form = new MultipartFormDataContent { { new StringContent("value"), "other" } };

        var response = await _client.PostAsync("/api/files", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.BlobStorage.Blobs);
    }

    [Fact]
    public async Task Upload_EmptyFile_Returns400()
    {
        var response = await _client.PostAsync("/api/files", FileForm("empty.txt", []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.BlobStorage.Blobs);
    }

    [Fact]
    public async Task Upload_DisallowedExtension_Returns400()
    {
        var response = await _client.PostAsync("/api/files", FileForm("run.exe", [1, 2, 3], "application/octet-stream"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.BlobStorage.Blobs);
    }

    [Fact]
    public async Task Upload_OversizedFile_Returns400()
    {
        var content = new byte[FileUploadsApiFactory.MaxFileSizeBytes + 1];

        var response = await _client.PostAsync("/api/files", FileForm("big.txt", content));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.BlobStorage.Blobs);
    }

    [Fact]
    public async Task Upload_DatabaseFailure_DeletesBlobAndReturns500()
    {
        _factory.FailDatabaseSaves = true;

        var response = await _client.PostAsync("/api/files", FileForm("notes.txt", [1, 2, 3]));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(_factory.BlobStorage.Blobs);
        Assert.Single(_factory.BlobStorage.Deleted);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/files/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AfterUpload_ReturnsRecord()
    {
        var upload = await _client.PostAsync("/api/files", FileForm("notes.txt", [1, 2, 3]));
        var created = await upload.Content.ReadFromJsonAsync<FileRecord>();

        var response = await _client.GetAsync(upload.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = await response.Content.ReadFromJsonAsync<FileRecord>();
        Assert.Equal(created!.Id, record!.Id);
    }
}
