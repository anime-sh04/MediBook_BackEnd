namespace MediBook.Auth.API.Helpers;

/// <summary>
/// Strongly-typed configuration for OAuth2 social login providers.
/// Bound from appsettings.json → "OAuthSettings" section.
///
/// Required appsettings entries:
///   "OAuthSettings": {
///     "Google": { "ClientId": "...", "ClientSecret": "..." },
///     "GitHub": { "ClientId": "...", "ClientSecret": "..." },
///     "CallbackBaseUrl": "https://your-api-host"
///   }
/// </summary>
public sealed class OAuthSettings
{
    public const string SectionName = "OAuthSettings";

    public GoogleOAuthSettings  Google          { get; init; } = new();
    public GitHubOAuthSettings  GitHub          { get; init; } = new();

    /// <summary>
    /// Base URL of this auth-service, used to build absolute callback URIs.
    /// E.g. "https://auth.medibook.io" or "http://localhost:5000" in dev.
    /// </summary>
    public string CallbackBaseUrl { get; init; } = string.Empty;
}

public sealed class GoogleOAuthSettings
{
    public string ClientId     { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
}

public sealed class GitHubOAuthSettings
{
    public string ClientId     { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
}
