namespace MediBook.Auth.API.Entities;

/// <summary>
/// Persisted refresh token — stored in the database, linked to a User.
/// A refresh token is a long-lived opaque string used to obtain a new
/// short-lived JWT access token without re-entering credentials.
///
/// Security properties:
///   - 64-byte cryptographically random value (512-bit entropy)
///   - Can be revoked individually or in bulk on logout / suspicious activity
///   - Expires after a configurable window (default 7 days)
///   - Tracks the IP and User-Agent that created it for audit purposes
/// </summary>
public class RefreshToken
{
    public Guid Id { get; private set; }

    /// <summary>FK → users.id</summary>
    public Guid UserId { get; private set; }

    /// <summary>512-bit random hex string — the opaque token value sent to client.</summary>
    public string Token { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public bool IsRevoked { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    /// <summary>IP address of the client that obtained this token.</summary>
    public string? CreatedByIp { get; private set; }

    // EF Core parameterless constructor
    private RefreshToken() { }

    /// <summary>
    /// Factory — generates a cryptographically secure random token.
    /// </summary>
    public static RefreshToken Create(Guid userId, int expiryDays = 7, string? ipAddress = null)
    {
        return new RefreshToken
        {
            Id           = Guid.NewGuid(),
            UserId       = userId,
            Token        = GenerateSecureToken(),
            ExpiresAt    = DateTime.UtcNow.AddDays(expiryDays),
            CreatedAt    = DateTime.UtcNow,
            IsRevoked    = false,
            CreatedByIp  = ipAddress
        };
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsActive => !IsRevoked && !IsExpired;

    public void Revoke(string reason = "Revoked")
    {
        IsRevoked     = true;
        RevokedAt     = DateTime.UtcNow;
        RevokedReason = reason;
    }

    private static string GenerateSecureToken()
    {
        // 64 bytes = 512 bits of entropy → URL-safe Base64 string
        var bytes = new byte[64];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
               .Replace("+", "-")
               .Replace("/", "_")
               .TrimEnd('=');
    }
}
