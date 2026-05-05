using MediBook.Provider.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediBook.Provider.API.Data;

public class ProviderDbContext : DbContext
{
    public ProviderDbContext(DbContextOptions<ProviderDbContext> options) : base(options) { }

    public DbSet<ProviderProfile> ProviderProfiles => Set<ProviderProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProviderProfile>(entity =>
        {
            entity.ToTable("provider_profiles");

            entity.HasKey(p => p.ProviderId);
            
            entity.Property(p => p.ProviderId)
                  .HasColumnName("provider_id")
                  .ValueGeneratedNever();

            entity.Property(p => p.UserId)
                  .HasColumnName("user_id")
                  .IsRequired();

            entity.HasIndex(p => p.UserId)
                  .IsUnique()
                  .HasDatabaseName("ix_provider_profiles_user_id");

            entity.Property(p => p.Specialization)
                  .HasColumnName("specialization")
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(p => p.Qualification)
                  .HasColumnName("qualification")
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(p => p.ExperienceYears)
                  .HasColumnName("experience_years")
                  .IsRequired();

            entity.Property(p => p.Bio)
                  .HasColumnName("bio")
                  .HasMaxLength(1000);

            entity.Property(p => p.ClinicName)
                  .HasColumnName("clinic_name")
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(p => p.ClinicAddress)
                  .HasColumnName("clinic_address")
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(p => p.City)
                  .HasColumnName("city")
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(p => p.State)
                  .HasColumnName("state")
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(p => p.ConsultationFee)
                  .HasColumnName("consultation_fee")
                  .HasColumnType("numeric(18,2)")
                  .IsRequired();

            entity.Property(p => p.IsVerified)
                  .HasColumnName("is_verified")
                  .HasDefaultValue(false);

            entity.Property(p => p.IsAvailable)
                  .HasColumnName("is_available")
                  .HasDefaultValue(true);

            entity.Property(p => p.AvgRating)
                  .HasColumnName("avg_rating")
                  .HasDefaultValue(0.0);

            entity.Property(p => p.CreatedAt)
                  .HasColumnName("created_at")
                  .HasColumnType("timestamp with time zone");

            entity.Property(p => p.UpdatedAt)
                  .HasColumnName("updated_at")
                  .HasColumnType("timestamp with time zone")
                  .IsRequired(false);
        });
    }
}
