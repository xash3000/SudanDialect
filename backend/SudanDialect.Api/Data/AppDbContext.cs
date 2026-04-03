using Microsoft.EntityFrameworkCore;
using SudanDialect.Api.Models;

namespace SudanDialect.Api.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Word> Words => Set<Word>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<Word>(entity =>
        {
            entity.ToTable("words");

            entity.HasKey(word => word.Id);

            entity.Property(word => word.Headword)
                .IsRequired();

            entity.Property(word => word.NormalizedHeadword)
                .IsRequired();

            entity.Property(word => word.Definition)
                .IsRequired();

            entity.Property(word => word.NormalizedDefinition)
                .IsRequired();

            entity.Property(word => word.IsActive)
                .HasDefaultValue(true);

            entity.Property(word => word.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.Property(word => word.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.HasIndex(word => word.NormalizedHeadword)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            entity.HasIndex(word => word.NormalizedDefinition)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
        });
    }

    public override int SaveChanges()
    {
        ApplyTimestamps();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Word>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = utcNow;
                }

                entry.Entity.UpdatedAt = utcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }
    }
}
