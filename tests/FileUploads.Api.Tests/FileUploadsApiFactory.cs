using FileUploads.Api.Data;
using FileUploads.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FileUploads.Api.Tests;

/// <summary>Hosts the API with an in-memory database and a fake blob store, so tests never reach Azure or PostgreSQL.</summary>
public sealed class FileUploadsApiFactory : WebApplicationFactory<Program>
{
    public const long MaxFileSizeBytes = 1024;

    private readonly string _databaseName = Guid.NewGuid().ToString();

    public FakeBlobStorageService BlobStorage { get; } = new();

    /// <summary>When true, every SaveChanges call throws, simulating a database outage.</summary>
    public bool FailDatabaseSaves { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Upload:MaxFileSizeBytes", MaxFileSizeBytes.ToString());

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options
                .UseInMemoryDatabase(_databaseName)
                .AddInterceptors(new FailingSaveInterceptor(this)));

            services.RemoveAll<IBlobStorageService>();
            services.AddSingleton<IBlobStorageService>(BlobStorage);
        });
    }

    private sealed class FailingSaveInterceptor(FileUploadsApiFactory factory) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            factory.FailDatabaseSaves
                ? throw new InvalidOperationException("Simulated database failure.")
                : base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
