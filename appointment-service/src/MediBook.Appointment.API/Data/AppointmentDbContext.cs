using MediBook.Appointment.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediBook.Appointment.API.Data;

public sealed class AppointmentDbContext : DbContext
{
    public AppointmentDbContext(DbContextOptions<AppointmentDbContext> options) : base(options) { }

    public DbSet<Entities.Appointment> Appointments => Set<Entities.Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Entities.Appointment>(entity =>
        {
            entity.ToTable("appointments");

            entity.HasKey(a => a.AppointmentId);

            entity.Property(a => a.AppointmentId)
                  .HasColumnName("appointment_id")
                  .UseIdentityAlwaysColumn();

            entity.Property(a => a.PatientId)
                  .HasColumnName("patient_id")
                  .HasColumnType("uuid")
                  .IsRequired();

            entity.Property(a => a.ProviderId)
                  .HasColumnName("provider_id")
                  .HasColumnType("uuid")
                  .IsRequired();

            entity.Property(a => a.SlotId)
                  .HasColumnName("slot_id")
                  .IsRequired();

            entity.Property(a => a.ServiceType)
                  .HasColumnName("service_type")
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(a => a.AppointmentDate)
                  .HasColumnName("appointment_date")
                  .HasColumnType("date")
                  .IsRequired();

            entity.Property(a => a.StartTime)
                  .HasColumnName("start_time")
                  .HasColumnType("time without time zone")
                  .IsRequired();

            entity.Property(a => a.EndTime)
                  .HasColumnName("end_time")
                  .HasColumnType("time without time zone")
                  .IsRequired();

            entity.Property(a => a.Status)
                  .HasColumnName("status")
                  .HasMaxLength(20)
                  .HasDefaultValue(Entities.Appointment.StatusScheduled)
                  .IsRequired();

            entity.Property(a => a.Notes)
                  .HasColumnName("notes")
                  .HasMaxLength(2000)
                  .HasDefaultValue(string.Empty);

            entity.Property(a => a.ModeOfConsultation)
                  .HasColumnName("mode_of_consultation")
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(a => a.CreatedAt)
                  .HasColumnName("created_at")
                  .HasColumnType("timestamp with time zone");

            entity.Property(a => a.UpdatedAt)
                  .HasColumnName("updated_at")
                  .HasColumnType("timestamp with time zone");

            // ── Indexes ───────────────────────────────────────────────────────
            entity.HasIndex(a => a.PatientId)
                  .HasDatabaseName("ix_appointments_patient_id");

            entity.HasIndex(a => a.ProviderId)
                  .HasDatabaseName("ix_appointments_provider_id");

            entity.HasIndex(a => a.SlotId)
                  .IsUnique()
                  .HasDatabaseName("ix_appointments_slot_id");          // one booking per slot

            entity.HasIndex(a => a.Status)
                  .HasDatabaseName("ix_appointments_status");

            entity.HasIndex(a => new { a.ProviderId, a.AppointmentDate })
                  .HasDatabaseName("ix_appointments_provider_date");

            entity.HasIndex(a => new { a.PatientId, a.AppointmentDate })
                  .HasDatabaseName("ix_appointments_patient_date");
        });
    }
}
