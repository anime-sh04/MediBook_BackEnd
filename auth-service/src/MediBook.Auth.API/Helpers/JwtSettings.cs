namespace MediBook.Auth.API.Helpers;

/// <summary>
/// Strongly-typed configuration for JWT generation.
/// Bound from appsettings.json → "JwtSettings" section.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    /// <summary>
    /// Secret key used to sign JWT tokens.
    /// MUST be at least 32 characters (256-bit).
    /// In production, store in environment variable / Azure Key Vault — never in source.
    /// </summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Token issuer (iss claim) — e.g. "MediBook.Auth"</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Intended audience (aud claim) — e.g. "MediBook.Client"</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>Access token lifetime in minutes. Default: 60 (1 hour).</summary>
    public int AccessTokenExpiryMinutes { get; init; } = 60;

    /// <summary>Refresh token lifetime in days. Default: 7.</summary>
    public int RefreshTokenExpiryDays { get; init; } = 7;
}
