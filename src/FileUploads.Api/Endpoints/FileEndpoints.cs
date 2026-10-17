using Azure;
using Azure.Identity;
using FileUploads.Api.Data;
using FileUploads.Api.Options;
using FileUploads.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FileUploads.Api.Endpoints;

public static class FileEndpoints
{
    private const int MaxOriginalFileNameLength = 255;

    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/files").WithTags("Files");

        group.MapPost("/", UploadAsync)
            .DisableAntiforgery()
            .WithName("UploadFile");

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetFile");

        group.MapGet("/", ListAsync)
            .WithName("ListFiles");

        return app;
    }

    private static async Task<IResult> UploadAsync(
        IFormFile? file,
        IBlobStorageService blobStorage,
        AppDbContext db,
        IOptions<UploadOptions> uploadOptions,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var options = uploadOptions.Value;

        if (file is null || file.Length == 0)
        {
            return Results.Problem("A non-empty file must be provided in the 'file' form field.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > options.MaxFileSizeBytes)
        {
            return Results.Problem($"File exceeds the maximum allowed size of {options.MaxFileSizeBytes} bytes.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Only the file name is kept; any client-supplied directory parts are discarded.
        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return Results.Problem($"File type '{extension}' is not allowed.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (originalFileName.Length > MaxOriginalFileNameLength)
        {
            originalFileName = originalFileName[..(MaxOriginalFileNameLength - extension.Length)] + extension;
        }

        var logger = loggerFactory.CreateLogger(typeof(FileEndpoints));
        var id = Guid.NewGuid();
        var blobName = $"{id:N}{extension}";
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

        Uri blobUri;
        try
        {
            await using var stream = file.OpenReadStream();
            blobUri = await blobStorage.UploadAsync(blobName, stream, contentType, cancellationToken);
        }
        catch (Exception ex) when (ex is RequestFailedException or AuthenticationFailedException)
        {
            logger.LogError(ex, "Uploading blob {BlobName} failed", blobName);
            return Results.Problem("Failed to store the file in Azure Blob Storage.", statusCode: StatusCodes.Status502BadGateway);
        }

        var record = new FileRecord
        {
            Id = id,
            OriginalFileName = originalFileName,
            BlobName = blobName,
            SizeBytes = file.Length,
            ContentType = contentType,
            UploadedAtUtc = DateTime.UtcNow,
            BlobUrl = blobUri.ToString(),
        };

        try
        {
            db.FileRecords.Add(record);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saving metadata for blob {BlobName} failed; removing the orphaned blob", blobName);
            try
            {
                await blobStorage.DeleteIfExistsAsync(blobName, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                logger.LogError(cleanupEx, "Cleanup of orphaned blob {BlobName} failed; it must be removed manually", blobName);
            }

            return Results.Problem("Failed to save file metadata.", statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.CreatedAtRoute("GetFile", new { id = record.Id }, record);
    }

    private static async Task<IResult> GetByIdAsync(Guid id, AppDbContext db, CancellationToken cancellationToken)
    {
        var record = await db.FileRecords.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        return record is null ? Results.NotFound() : Results.Ok(record);
    }

    private static async Task<IResult> ListAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var records = await db.FileRecords.AsNoTracking()
            .OrderByDescending(f => f.UploadedAtUtc)
            .ToListAsync(cancellationToken);
        return Results.Ok(records);
    }
}
