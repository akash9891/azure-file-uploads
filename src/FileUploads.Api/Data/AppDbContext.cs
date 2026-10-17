using Microsoft.EntityFrameworkCore;

namespace FileUploads.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<FileRecord> FileRecords => Set<FileRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var file = modelBuilder.Entity<FileRecord>();
        file.HasKey(f => f.Id);
        file.Property(f => f.OriginalFileName).HasMaxLength(255).IsRequired();
        file.Property(f => f.BlobName).HasMaxLength(100).IsRequired();
        file.HasIndex(f => f.BlobName).IsUnique();
        file.Property(f => f.ContentType).HasMaxLength(255).IsRequired();
        file.Property(f => f.BlobUrl).HasMaxLength(2048).IsRequired();
    }
}
