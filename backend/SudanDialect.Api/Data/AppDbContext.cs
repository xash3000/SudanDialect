using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SudanDialect.Api.Models;

namespace SudanDialect.Api.Data;

public sealed class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Word> Words => Set<Word>();
    public DbSet<Audit> Audits => Set<Audit>();
    public DbSet<Feedback> Feedback => Set<Feedback>();
    public DbSet<WordSuggestion> WordSuggestions => Set<WordSuggestion>();

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

        modelBuilder.Entity<Audit>(entity =>
        {
            entity.HasKey(audit => audit.Id);

            entity.Property(audit => audit.AdminUserId)
                .IsRequired();

            entity.Property(audit => audit.EditedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.Property(audit => audit.ActionType)
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(audit => audit.OldHeadword)
                .IsRequired();

            entity.Property(audit => audit.NewHeadword)
                .IsRequired();

            entity.Property(audit => audit.OldDefinition)
                .IsRequired();

            entity.Property(audit => audit.NewDefinition)
                .IsRequired();

            entity.Property(audit => audit.OldNormalizedHeadword)
                .IsRequired();

            entity.Property(audit => audit.NewNormalizedHeadword)
                .IsRequired();

            entity.Property(audit => audit.OldNormalizedDefinition)
                .IsRequired();

            entity.Property(audit => audit.NewNormalizedDefinition)
                .IsRequired();

            entity.Property(audit => audit.ClientIp)
                .HasMaxLength(100);

            entity.Property(audit => audit.UserAgent)
                .HasMaxLength(512);

            entity.HasOne(audit => audit.Word)
                .WithMany()
                .HasForeignKey(audit => audit.WordId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(audit => new { audit.WordId, audit.EditedAt });
            entity.HasIndex(audit => new { audit.AdminUserId, audit.EditedAt });
            entity.HasIndex(audit => audit.EditedAt);
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.ToTable("feedback");

            entity.HasKey(feedback => feedback.Id);

            entity.Property(feedback => feedback.FeedbackText)
                .HasColumnName("feedback_text")
                .IsRequired();

            entity.Property(feedback => feedback.Resolved)
                .HasColumnName("resolved")
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(feedback => feedback.Timestamp)
                .HasColumnName("timestamp")
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.HasOne(feedback => feedback.Word)
                .WithMany()
                .HasForeignKey(feedback => feedback.WordId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(feedback => new { feedback.WordId, feedback.Timestamp });
        });

        modelBuilder.Entity<WordSuggestion>(entity =>
        {
            entity.ToTable("word_suggestions");

            entity.HasKey(suggestion => suggestion.Id);

            entity.Property(suggestion => suggestion.Headword)
                .HasColumnName("headword")
                .IsRequired();

            entity.Property(suggestion => suggestion.Definition)
                .HasColumnName("definition")
                .IsRequired();

            entity.Property(suggestion => suggestion.Email)
                .HasColumnName("email")
                .HasMaxLength(320);

            entity.Property(suggestion => suggestion.Resolved)
                .HasColumnName("resolved")
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(suggestion => suggestion.Timestamp)
                .HasColumnName("timestamp")
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.HasIndex(suggestion => suggestion.Timestamp);
            entity.HasIndex(suggestion => new { suggestion.Resolved, suggestion.Timestamp });
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

        foreach (var entry in ChangeTracker.Entries<Feedback>())
        {
            if (entry.State == EntityState.Added && entry.Entity.Timestamp == default)
            {
                entry.Entity.Timestamp = utcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<WordSuggestion>())
        {
            if (entry.State == EntityState.Added && entry.Entity.Timestamp == default)
            {
                entry.Entity.Timestamp = utcNow;
            }
        }
    }
}
