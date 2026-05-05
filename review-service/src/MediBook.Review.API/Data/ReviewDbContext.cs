using Microsoft.EntityFrameworkCore;

namespace MediBook.Review.API.Data;

public sealed class ReviewDbContext : DbContext
{
    public ReviewDbContext(DbContextOptions<ReviewDbContext> options) : base(options) { }

    public DbSet<Entities.Review> Reviews => Set<Entities.Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Entities.Review>(entity =>
        {
            entity.ToTable("reviews");

            entity.HasKey(r => r.ReviewId);

            entity.Property(r => r.ReviewId)
                  .HasColumnName("review_id")
                  .UseIdentityAlwaysColumn();

            // ── Unique: one review per appointment ────────────────────────────
            entity.Property(r => r.AppointmentId)
                  .HasColumnName("appointment_id")
                  .IsRequired();

            entity.HasIndex(r => r.AppointmentId)
                  .IsUnique()
                  .HasDatabaseName("ix_reviews_appointment_id_unique");

            entity.Property(r => r.PatientId)
                  .HasColumnName("patient_id")
                  .HasColumnType("uuid")
                  .IsRequired();

            entity.Property(r => r.ProviderId)
                  .HasColumnName("provider_id")
                  .HasColumnType("uuid")
                  .IsRequired();

            entity.Property(r => r.Rating)
                  .HasColumnName("rating")
                  .IsRequired();

            // DB-level check constraint: rating between 1 and 5
            entity.ToTable(t => t.HasCheckConstraint("ck_reviews_rating", "rating BETWEEN 1 AND 5"));

            entity.Property(r => r.Comment)
                  .HasColumnName("comment")
                  .HasMaxLength(2000)
                  .IsRequired();

            entity.Property(r => r.ReviewDate)
                  .HasColumnName("review_date")
                  .HasColumnType("date")
                  .IsRequired();

            entity.Property(r => r.IsVerified)
                  .HasColumnName("is_verified")
                  .HasDefaultValue(false);

            entity.Property(r => r.IsAnonymous)
                  .HasColumnName("is_anonymous")
                  .HasDefaultValue(false);

            // ── Indexes ───────────────────────────────────────────────────────
            entity.HasIndex(r => r.ProviderId)
                  .HasDatabaseName("ix_reviews_provider_id");

            entity.HasIndex(r => r.PatientId)
                  .HasDatabaseName("ix_reviews_patient_id");

            entity.HasIndex(r => r.Rating)
                  .HasDatabaseName("ix_reviews_rating");
        });
    }
}
