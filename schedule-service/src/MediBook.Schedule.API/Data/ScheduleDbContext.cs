using MediBook.Schedule.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediBook.Schedule.API.Data;

public sealed class ScheduleDbContext : DbContext
{
    public ScheduleDbContext(DbContextOptions<ScheduleDbContext> options) : base(options) { }

    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();

    /// <summary>Shorthand alias used by the Saga consumer for direct slot lookups.</summary>
    public DbSet<AvailabilitySlot> Slots => Set<AvailabilitySlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AvailabilitySlot>(entity =>
        {
            entity.ToTable("availability_slots");

            entity.HasKey(s => s.SlotId);

            entity.Property(s => s.SlotId)
                  .HasColumnName("slot_id")
                  .UseIdentityAlwaysColumn();

            entity.Property(s => s.ProviderId)
                  .HasColumnName("provider_id")
                  .IsRequired();

            entity.Property(s => s.Date)
                  .HasColumnName("date")
                  .HasColumnType("date")
                  .IsRequired();

            entity.Property(s => s.StartTime)
                  .HasColumnName("start_time")
                  .HasColumnType("time without time zone")
                  .IsRequired();

            entity.Property(s => s.EndTime)
                  .HasColumnName("end_time")
                  .HasColumnType("time without time zone")
                  .IsRequired();

            entity.Property(s => s.DurationMinutes)
                  .HasColumnName("duration_minutes")
                  .IsRequired();

            entity.Property(s => s.IsBooked)
                  .HasColumnName("is_booked")
                  .HasDefaultValue(false);

            entity.Property(s => s.IsBlocked)
                  .HasColumnName("is_blocked")
                  .HasDefaultValue(false);

            entity.Property(s => s.Recurrence)
                  .HasColumnName("recurrence")
                  .HasMaxLength(20)
                  .HasDefaultValue("none");

            entity.Property(s => s.CreatedAt)
                  .HasColumnName("created_at")
                  .HasColumnType("timestamp with time zone");

            // Indexes for the most common query patterns
            entity.HasIndex(s => s.ProviderId)
                  .HasDatabaseName("ix_availability_slots_provider_id");

            entity.HasIndex(s => new { s.ProviderId, s.Date })
                  .HasDatabaseName("ix_availability_slots_provider_date");

            entity.HasIndex(s => new { s.ProviderId, s.IsBooked, s.IsBlocked })
                  .HasDatabaseName("ix_availability_slots_provider_available");
            
            entity.Property(s => s.Price)
                  .HasColumnName("Price")
                  .HasColumnType("numeric(10,2)")
                  .IsRequired();
        });
    }
}

