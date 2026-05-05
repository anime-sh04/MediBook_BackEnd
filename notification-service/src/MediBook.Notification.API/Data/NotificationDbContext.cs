using MediBook.Notification.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediBook.Notification.API.Data;

public sealed class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options) { }

    public DbSet<Entities.Notification> Notifications => Set<Entities.Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Entities.Notification>(entity =>
        {
            entity.ToTable("notifications");

            entity.HasKey(n => n.Id);

            entity.Property(n => n.Id)
                  .HasColumnName("id")
                  .ValueGeneratedNever();

            entity.Property(n => n.RecipientId)
                  .HasColumnName("recipient_id")
                  .IsRequired();

            entity.Property(n => n.Type)
                  .HasColumnName("type")
                  .HasMaxLength(30)
                  .IsRequired();

            entity.Property(n => n.Title)
                  .HasColumnName("title")
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(n => n.Message)
                  .HasColumnName("message")
                  .HasMaxLength(2000)
                  .IsRequired();

            entity.Property(n => n.Channel)
                  .HasColumnName("channel")
                  .HasMaxLength(10)
                  .IsRequired();

            entity.Property(n => n.RelatedId)
                  .HasColumnName("related_id");

            entity.Property(n => n.RelatedType)
                  .HasColumnName("related_type")
                  .HasMaxLength(50);

            entity.Property(n => n.IsRead)
                  .HasColumnName("is_read")
                  .HasDefaultValue(false);

            entity.Property(n => n.SentAt)
                  .HasColumnName("sent_at");

            entity.Property(n => n.CreatedAt)
                  .HasColumnName("created_at");

            // Indexes for common query patterns
            entity.HasIndex(n => n.RecipientId)
                  .HasDatabaseName("ix_notifications_recipient_id");

            entity.HasIndex(n => new { n.RecipientId, n.IsRead })
                  .HasDatabaseName("ix_notifications_recipient_is_read");

            entity.HasIndex(n => n.Type)
                  .HasDatabaseName("ix_notifications_type");

            entity.HasIndex(n => n.RelatedId)
                  .HasDatabaseName("ix_notifications_related_id");
        });
    }
}
