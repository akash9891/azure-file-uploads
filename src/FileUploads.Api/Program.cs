using Azure.Identity;
using Azure.Storage.Blobs;
using FileUploads.Api.Data;
using FileUploads.Api.Endpoints;
using FileUploads.Api.Options;
using FileUploads.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddOptions<AzureStorageOptions>()
    .Bind(builder.Configuration.GetSection(AzureStorageOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.AccountName), "AzureStorage:AccountName is missing.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ContainerName), "AzureStorage:ContainerName is missing.");

builder.Services.AddOptions<UploadOptions>()
    .Bind(builder.Configuration.GetSection(UploadOptions.SectionName))
    .Validate(o => o.MaxFileSizeBytes > 0, "Upload:MaxFileSizeBytes must be positive.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// DefaultAzureCredential uses the Azure CLI login locally and a Managed Identity when deployed.
// Managed Identity is skipped in Development: off Azure its probe can fail hard and stop the chain
// before it reaches the developer's Azure CLI login.
var credentialOptions = new DefaultAzureCredentialOptions
{
    ExcludeManagedIdentityCredential = builder.Environment.IsDevelopment(),
};

builder.Services.AddSingleton(sp =>
{
    var storage = sp.GetRequiredService<IOptions<AzureStorageOptions>>().Value;
    var serviceClient = new BlobServiceClient(
        new Uri($"https://{storage.AccountName}.blob.core.windows.net"),
        new DefaultAzureCredential(credentialOptions));
    return serviceClient.GetBlobContainerClient(storage.ContainerName);
});
builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

var app = builder.Build();

// Off by default; docker-compose turns it on so a fresh database gets its schema without the dotnet-ef tool.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapFileEndpoints();

app.Run();

public partial class Program;
