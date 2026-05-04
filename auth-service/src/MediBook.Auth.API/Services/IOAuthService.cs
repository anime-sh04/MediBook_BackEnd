using MediBook.Auth.API.DTOs;

namespace MediBook.Auth.API.Services;

/// <summary>
/// Business contract for OAuth2 social-login flows (Google, GitHub).
///
/// Flow overview:
///   1. Client calls GET /api/v1/auth/oauth/{provider}/login
///      → Service returns the provider's authorization URL with state + nonce.
///   2. User authenticates on provider's consent screen.
///   3. Provider redirects to GET /api/v1/auth/oauth/{provider}/callback?code=...&state=...
///      → Service exchanges code for profile, upserts user, returns JWT pair.
/// </summary>
public interface IOAuthService
{
    /// <summary>
    /// Builds the authorization URL to redirect the user to the given OAuth provider.
    /// Generates and stores a CSRF state token for later validation.
    /// </summary>
    /// <param name="provider">"google" or "github" (case-insensitive)</param>
    /// <param name="ipAddress">Caller IP, stored for audit.</param>
    /// <returns>The provider's authorization URL the client should redirect to.</returns>
    /// <exception cref="ArgumentException">Unknown provider.</exception>
    Task<string> GetAuthorizationUrlAsync(string provider, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Handles the OAuth callback: validates state, exchanges code for tokens,
    /// fetches the user's profile, upserts the MediBook User, issues JWT pair.
    /// </summary>
    /// <param name="provider">"google" or "github"</param>
    /// <param name="code">Authorization code from the provider.</param>
    /// <param name="state">State token returned by provider (must match stored value).</param>
    /// <param name="ipAddress">Caller IP for refresh token audit.</param>
    /// <returns>JWT access token + refresh token + user info + isNewUser flag.</returns>
    /// <exception cref="UnauthorizedAccessException">State mismatch / invalid code.</exception>
    Task<OAuthLoginResponse> HandleCallbackAsync(
        string  provider,
        string  code,
        string  state,
        string? ipAddress,
        CancellationToken ct = default);
}
