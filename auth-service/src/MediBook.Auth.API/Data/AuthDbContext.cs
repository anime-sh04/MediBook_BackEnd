using MediBook.Auth.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediBook.Auth.API.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<User>         Users         => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();  // UC-2

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(u => u.Id);

            entity.Property(u => u.Id)
                  .HasColumnName("id")
                  .ValueGeneratedNever();

            entity.Property(u => u.FullName)
                  .HasColumnName("full_name")
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(u => u.Email)
                  .HasColumnName("email")
                  .HasMaxLength(320)
                  .IsRequired();

            entity.HasIndex(u => u.Email)
                  .IsUnique()
                  .HasDatabaseName("ix_users_email");

            entity.Property(u => u.PasswordHash)
                  .HasColumnName("password_hash")
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(u => u.Phone)
                  .HasColumnName("phone")
                  .HasMaxLength(20);

            entity.Property(u => u.Role)
                  .HasColumnName("role")
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(u => u.IsActive)
                  .HasColumnName("is_active")
                  .HasDefaultValue(true);

            entity.Property(u => u.CreatedAt)
                  .HasColumnName("created_at")
                  .HasColumnType("timestamp with time zone");

            entity.Property(u => u.UpdatedAt)
                  .HasColumnName("updated_at")
                  .HasColumnType("timestamp with time zone")
                  .IsRequired(false);

            entity.Property(u => u.ProfilePicUrl)
                  .HasColumnName("profile_pic_url")
                  .HasMaxLength(1000)
                  .IsRequired(false);

            // ── OAuth fields ───────────────────────────────────────────────────
            entity.Property(u => u.OAuthProvider)
                  .HasColumnName("oauth_provider")
                  .HasMaxLength(50)
                  .IsRequired(false);

            entity.Property(u => u.OAuthProviderId)
                  .HasColumnName("oauth_provider_id")
                  .HasMaxLength(200)
                  .IsRequired(false);

            // Composite index: fast lookup of returning OAuth users by (provider, id)
            entity.HasIndex(u => new { u.OAuthProvider, u.OAuthProviderId })
                  .HasDatabaseName("ix_users_oauth_provider_id")
                  .IsUnique(false);   // not unique — allows null rows on both cols

            // Navigation: User → RefreshTokens (one-to-many)
            entity.HasMany(u => u.RefreshTokens)
                  .WithOne()
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Private backing field for EF to populate the collection
            entity.Navigation(u => u.RefreshTokens)
                  .UsePropertyAccessMode(PropertyAccessMode.Field)
                  .HasField("_refreshTokens");
        });

        // ── RefreshToken ─────────────────────────────────────────────────────
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");

            entity.HasKey(rt => rt.Id);

            entity.Property(rt => rt.Id)
                  .HasColumnName("id")
                  .ValueGeneratedNever();

            entity.Property(rt => rt.UserId)
                  .HasColumnName("user_id")
                  .IsRequired();

            entity.Property(rt => rt.Token)
                  .HasColumnName("token")
                  .HasMaxLength(512)
                  .IsRequired();

            // The token itself must be unique (used for lookups on refresh)
            entity.HasIndex(rt => rt.Token)
                  .IsUnique()
                  .HasDatabaseName("ix_refresh_tokens_token");

            entity.HasIndex(rt => rt.UserId)
                  .HasDatabaseName("ix_refresh_tokens_user_id");

            entity.Property(rt => rt.ExpiresAt)
                  .HasColumnName("expires_at")
                  .HasColumnType("timestamp with time zone");

            entity.Property(rt => rt.CreatedAt)
                  .HasColumnName("created_at")
                  .HasColumnType("timestamp with time zone");

            entity.Property(rt => rt.IsRevoked)
                  .HasColumnName("is_revoked")
                  .HasDefaultValue(false);

            entity.Property(rt => rt.RevokedAt)
                  .HasColumnName("revoked_at")
                  .HasColumnType("timestamp with time zone")
                  .IsRequired(false);

            entity.Property(rt => rt.RevokedReason)
                  .HasColumnName("revoked_reason")
                  .HasMaxLength(200)
                  .IsRequired(false);

            entity.Property(rt => rt.CreatedByIp)
                  .HasColumnName("created_by_ip")
                  .HasMaxLength(45)   // IPv6 max length
                  .IsRequired(false);
        });
    }
}


