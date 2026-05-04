namespace MediBook.Auth.API.Entities;

/// <summary>
/// Core user entity for the Auth service.
///
/// Supports two authentication modes:
///   1. Local  — email + BCrypt-hashed password (PasswordHash is set)
///   2. OAuth  — Google or GitHub (OAuthProvider + OAuthProviderId are set;
///               PasswordHash is empty string, never compared)
///
/// A user registered via OAuth can later set a password via PUT /auth/set-password.
/// </summary>
public class User
{
    public Guid     Id           { get; private set; }
    public string   FullName     { get; private set; } = string.Empty;
    public string   Email        { get; private set; } = string.Empty;

    /// <summary>
    /// BCrypt hash for local accounts.
    /// Empty string for pure-OAuth accounts (never tested against a password).
    /// </summary>
    public string   PasswordHash { get; private set; } = string.Empty;

    public string   Phone        { get; private set; } = string.Empty;

    /// <summary>"Patient" | "Provider" | "Admin"</summary>
    public string   Role         { get; private set; } = string.Empty;

    public bool     IsActive     { get; private set; }
    public DateTime CreatedAt    { get; private set; }
    public DateTime? UpdatedAt   { get; private set; }
    public string?  ProfilePicUrl { get; private set; }

    // ── OAuth fields ──────────────────────────────────────────────────────────

    /// <summary>
    /// Identifies which OAuth provider created / linked this account.
    /// Values: null (local account) | "Google" | "GitHub"
    /// </summary>
    public string? OAuthProvider   { get; private set; }

    /// <summary>
    /// The provider's stable user identifier (Google "sub", GitHub user id).
    /// Used to look up returning OAuth users without relying on email alone.
    /// Unique per provider — indexed as (OAuthProvider, OAuthProviderId).
    /// </summary>
    public string? OAuthProviderId { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    private readonly List<RefreshToken> _refreshTokens = new();
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    // EF Core parameterless constructor
    private User() { }

    // ── Factories ─────────────────────────────────────────────────────────────

    /// <summary>Creates a local (email + password) account.</summary>
    public static User Create(
        string fullName,
        string email,
        string passwordHash,
        string phone,
        string role = "Patient")
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        return new User
        {
            Id           = Guid.NewGuid(),
            FullName     = fullName.Trim(),
            Email        = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Phone        = phone?.Trim() ?? string.Empty,
            Role         = role,
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates an OAuth account.
    /// PasswordHash is set to empty string — the account has no local password
    /// until the user explicitly sets one via PUT /auth/set-password.
    /// </summary>
    public static User CreateOAuth(
        string fullName,
        string email,
        string oauthProvider,
        string oauthProviderId,
        string? profilePicUrl = null,
        string role = "Patient")
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(oauthProvider))
            throw new ArgumentException("OAuth provider is required.", nameof(oauthProvider));
        if (string.IsNullOrWhiteSpace(oauthProviderId))
            throw new ArgumentException("OAuth provider ID is required.", nameof(oauthProviderId));

        return new User
        {
            Id              = Guid.NewGuid(),
            FullName        = fullName.Trim(),
            Email           = email.Trim().ToLowerInvariant(),
            PasswordHash    = string.Empty,   // no local password
            Phone           = string.Empty,
            Role            = role,
            IsActive        = true,
            CreatedAt       = DateTime.UtcNow,
            OAuthProvider   = oauthProvider,
            OAuthProviderId = oauthProviderId,
            ProfilePicUrl   = profilePicUrl
        };
    }

    // ── Computed ──────────────────────────────────────────────────────────────

    /// <summary>True if this user registered via an OAuth provider.</summary>
    public bool IsOAuthUser => !string.IsNullOrEmpty(OAuthProvider);

    /// <summary>True if this user has a local password set.</summary>
    public bool HasLocalPassword => !string.IsNullOrEmpty(PasswordHash);

    // ── Mutators ──────────────────────────────────────────────────────────────

    public void AddRefreshToken(RefreshToken token)
    {
        foreach (var existing in _refreshTokens.Where(rt => !rt.IsRevoked))
            existing.Revoke("Replaced by new login");
        _refreshTokens.Add(token);
        UpdatedAt = DateTime.UtcNow;
    }

    public bool RevokeRefreshToken(string tokenValue, string reason = "Explicit logout")
    {
        var token = _refreshTokens.FirstOrDefault(t => t.Token == tokenValue);
        if (token is null) return false;
        token.Revoke(reason);
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public void Deactivate()
    {
        IsActive  = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string fullName, string phone, string? profilePicUrl)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
            FullName = fullName.Trim();
        if (!string.IsNullOrWhiteSpace(phone))
            Phone = phone.Trim();
        ProfilePicUrl = profilePicUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangePasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash is required.", nameof(newPasswordHash));
        PasswordHash = newPasswordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Links an OAuth provider to an existing local account
    /// (e.g. user registered with email, later signs in with Google).
    /// </summary>
    public void LinkOAuthProvider(string provider, string providerId)
    {
        OAuthProvider   = provider;
        OAuthProviderId = providerId;
        UpdatedAt = DateTime.UtcNow;
    }
}
