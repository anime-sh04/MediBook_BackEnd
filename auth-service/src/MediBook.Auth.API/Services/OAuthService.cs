using System.Net.Http.Headers;
using System.Text.Json;
using MediBook.Auth.API.Data;
using MediBook.Auth.API.DTOs;
using MediBook.Auth.API.Entities;
using MediBook.Auth.API.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediBook.Auth.API.Services;

/// <summary>
/// Implements the OAuth2 authorization-code flow for Google and GitHub.
///
/// Design decisions:
///   • Uses raw HttpClient (via IHttpClientFactory) rather than the ASP.NET Core
///     cookie-based OAuth middleware because this is a pure API service — there
///     are no cookies / browser sessions. Tokens are returned as JSON, not set
///     as cookies.
///   • State tokens are stored in OAuthStateStore (in-memory; swap for Redis
///     in a multi-instance deployment).
///   • Provider profile is fetched with the provider's access token, then the
///     user is upserted: found by (OAuthProvider, OAuthProviderId) first,
///     then by email (to link an existing local account), then created fresh.
///   • On success, a MediBook JWT + refresh token pair is issued — same path
///     as local login.
/// </summary>
public sealed class OAuthService : IOAuthService
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private const string GoogleProvider = "google";
    private const string GitHubProvider = "github";

    // Google OAuth2 endpoints
    private const string GoogleAuthUrl      = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string GoogleTokenUrl     = "https://oauth2.googleapis.com/token";
    private const string GoogleUserInfoUrl  = "https://www.googleapis.com/oauth2/v3/userinfo";

    // GitHub OAuth2 endpoints
    private const string GitHubAuthUrl      = "https://github.com/login/oauth/authorize";
    private const string GitHubTokenUrl     = "https://github.com/login/oauth/access_token";
    private const string GitHubUserInfoUrl  = "https://api.github.com/user";
    private const string GitHubEmailsUrl    = "https://api.github.com/user/emails";

    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly AuthDbContext          _db;
    private readonly JwtTokenGenerator      _jwtGenerator;
    private readonly JwtSettings            _jwtSettings;
    private readonly OAuthSettings          _oauthSettings;
    private readonly OAuthStateStore        _stateStore;
    private readonly IHttpClientFactory     _httpClientFactory;
    private readonly ILogger<OAuthService>  _logger;

    public OAuthService(
        AuthDbContext              db,
        JwtTokenGenerator          jwtGenerator,
        IOptions<JwtSettings>      jwtSettings,
        IOptions<OAuthSettings>    oauthSettings,
        OAuthStateStore            stateStore,
        IHttpClientFactory         httpClientFactory,
        ILogger<OAuthService>      logger)
    {
        _db                = db;
        _jwtGenerator      = jwtGenerator;
        _jwtSettings       = jwtSettings.Value;
        _oauthSettings     = oauthSettings.Value;
        _stateStore        = stateStore;
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    // ── Step 1: Build authorization URL ──────────────────────────────────────

    public Task<string> GetAuthorizationUrlAsync(
        string provider, string? ipAddress, CancellationToken ct = default)
    {
        string normalised = provider.ToLowerInvariant();
        string state      = _stateStore.GenerateAndStore(normalised);
        string callbackUrl = BuildCallbackUrl(normalised);

        string url = normalised switch
        {
            GoogleProvider => BuildGoogleAuthUrl(state, callbackUrl),
            GitHubProvider => BuildGitHubAuthUrl(state, callbackUrl),
            _ => throw new ArgumentException($"Unknown OAuth provider: '{provider}'.")
        };

        _logger.LogInformation(
            "OAuth login initiated. Provider={Provider} IP={IP}", normalised, ipAddress);

        return Task.FromResult(url);
    }

    // ── Step 2: Handle callback ───────────────────────────────────────────────

    public async Task<OAuthLoginResponse> HandleCallbackAsync(
        string  provider,
        string  code,
        string  state,
        string? ipAddress,
        CancellationToken ct = default)
    {
        string normalised = provider.ToLowerInvariant();

        // ── 2a. Validate CSRF state ───────────────────────────────────────────
        if (!_stateStore.ValidateAndConsume(state, normalised))
        {
            _logger.LogWarning(
                "OAuth callback: invalid/expired state token. Provider={Provider}", normalised);
            throw new UnauthorizedAccessException(
                "Invalid or expired OAuth state token. Please try signing in again.");
        }

        // ── 2b. Exchange code → provider access token + user profile ─────────
        OAuthUserProfile profile = normalised switch
        {
            GoogleProvider => await FetchGoogleProfileAsync(code, normalised, ct),
            GitHubProvider => await FetchGitHubProfileAsync(code, normalised, ct),
            _ => throw new ArgumentException($"Unknown OAuth provider: '{provider}'.")
        };

        _logger.LogInformation(
            "OAuth profile fetched. Provider={Provider} ProviderId={ProviderId} Email={Email}",
            normalised, profile.ProviderId, profile.Email);

        // ── 2c. Upsert user ───────────────────────────────────────────────────
        var (user, isNewUser) = await UpsertUserAsync(profile, normalised, ipAddress, ct);

        // ── 2d. Issue MediBook JWT + refresh token ────────────────────────────
        var (accessToken, refreshToken, expiry) = await IssueTokensAsync(user, ipAddress, ct);

        _logger.LogInformation(
            "OAuth login success. UserId={UserId} IsNew={IsNew}", user.Id, isNewUser);

        return new OAuthLoginResponse(
            AccessToken:       accessToken,
            RefreshToken:      refreshToken,
            AccessTokenExpiry: expiry,
            User:              MapToUserDto(user),
            IsNewUser:         isNewUser);
    }

    // ── Google helpers ────────────────────────────────────────────────────────

    private string BuildGoogleAuthUrl(string state, string callbackUrl)
    {
        var qs = new Dictionary<string, string>
        {
            ["client_id"]     = _oauthSettings.Google.ClientId,
            ["redirect_uri"]  = callbackUrl,
            ["response_type"] = "code",
            ["scope"]         = "openid email profile",
            ["state"]         = state,
            ["access_type"]   = "online",
            ["prompt"]        = "select_account"   // force account chooser each time
        };
        return $"{GoogleAuthUrl}?{BuildQueryString(qs)}";
    }

    private async Task<OAuthUserProfile> FetchGoogleProfileAsync(
        string code, string provider, CancellationToken ct)
    {
        string callbackUrl = BuildCallbackUrl(provider);

        // Exchange authorization code for access token
        var tokenPayload = new Dictionary<string, string>
        {
            ["client_id"]     = _oauthSettings.Google.ClientId,
            ["client_secret"] = _oauthSettings.Google.ClientSecret,
            ["code"]          = code,
            ["grant_type"]    = "authorization_code",
            ["redirect_uri"]  = callbackUrl
        };

        var accessToken = await ExchangeCodeForTokenAsync(
            GoogleTokenUrl, tokenPayload, ct);

        // Fetch user profile from Google's userinfo endpoint
        using var client = _httpClientFactory.CreateClient("oauth");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var resp = await client.GetAsync(GoogleUserInfoUrl, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        string providerId = root.GetProperty("sub").GetString()
            ?? throw new InvalidOperationException("Google profile missing 'sub'.");
        string email = root.GetProperty("email").GetString()
            ?? throw new InvalidOperationException("Google profile missing 'email'.");
        string fullName = root.TryGetProperty("name", out var nameProp)
            ? nameProp.GetString() ?? email
            : email;
        string? picUrl = root.TryGetProperty("picture", out var picProp)
            ? picProp.GetString()
            : null;

        return new OAuthUserProfile(providerId, email, fullName, picUrl);
    }

    // ── GitHub helpers ────────────────────────────────────────────────────────

    private string BuildGitHubAuthUrl(string state, string callbackUrl)
    {
        var qs = new Dictionary<string, string>
        {
            ["client_id"]    = _oauthSettings.GitHub.ClientId,
            ["redirect_uri"] = callbackUrl,
            ["scope"]        = "read:user user:email",
            ["state"]        = state
        };
        return $"{GitHubAuthUrl}?{BuildQueryString(qs)}";
    }

    private async Task<OAuthUserProfile> FetchGitHubProfileAsync(
        string code, string provider, CancellationToken ct)
    {
        string callbackUrl = BuildCallbackUrl(provider);

        // Exchange authorization code for access token
        var tokenPayload = new Dictionary<string, string>
        {
            ["client_id"]     = _oauthSettings.GitHub.ClientId,
            ["client_secret"] = _oauthSettings.GitHub.ClientSecret,
            ["code"]          = code,
            ["redirect_uri"]  = callbackUrl
        };

        var accessToken = await ExchangeCodeForTokenAsync(
            GitHubTokenUrl, tokenPayload, ct);

        using var client = _httpClientFactory.CreateClient("oauth");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        // GitHub requires a User-Agent header
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MediBook-Auth/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        // ── Fetch user profile ────────────────────────────────────────────────
        var userResp = await client.GetAsync(GitHubUserInfoUrl, ct);
        userResp.EnsureSuccessStatusCode();

        using var userDoc = JsonDocument.Parse(await userResp.Content.ReadAsStringAsync(ct));
        var userRoot = userDoc.RootElement;

        string providerId = userRoot.GetProperty("id").GetRawText();   // numeric id
        string? login     = userRoot.TryGetProperty("login", out var loginProp)
            ? loginProp.GetString() : null;
        string? fullName  = userRoot.TryGetProperty("name", out var nameProp)
            ? nameProp.GetString() : null;
        string? picUrl    = userRoot.TryGetProperty("avatar_url", out var picProp)
            ? picProp.GetString() : null;

        // ── Fetch primary email (GitHub may not include it in the main profile) ─
        string email = await FetchGitHubPrimaryEmailAsync(client, userRoot, ct)
            ?? $"{login}@users.noreply.github.com";

        return new OAuthUserProfile(
            ProviderId:    providerId,
            Email:         email,
            FullName:      fullName ?? login ?? email,
            ProfilePicUrl: picUrl);
    }

    private static async Task<string?> FetchGitHubPrimaryEmailAsync(
        HttpClient client, JsonElement userRoot, CancellationToken ct)
    {
        // Try the email field on the profile first
        if (userRoot.TryGetProperty("email", out var emailProp) &&
            !string.IsNullOrWhiteSpace(emailProp.GetString()))
        {
            return emailProp.GetString();
        }

        // Fall back to the /user/emails endpoint
        var emailResp = await client.GetAsync(GitHubEmailsUrl, ct);
        if (!emailResp.IsSuccessStatusCode) return null;

        using var emailDoc = JsonDocument.Parse(await emailResp.Content.ReadAsStringAsync(ct));

        // Return the primary, verified email
        foreach (var emailEntry in emailDoc.RootElement.EnumerateArray())
        {
            bool primary  = emailEntry.TryGetProperty("primary",  out var pp) && pp.GetBoolean();
            bool verified = emailEntry.TryGetProperty("verified", out var vp) && vp.GetBoolean();
            if (primary && verified)
                return emailEntry.GetProperty("email").GetString();
        }

        // Fall back to any verified email
        foreach (var emailEntry in emailDoc.RootElement.EnumerateArray())
        {
            bool verified = emailEntry.TryGetProperty("verified", out var vp) && vp.GetBoolean();
            if (verified)
                return emailEntry.GetProperty("email").GetString();
        }

        return null;
    }

    // ── Token exchange ────────────────────────────────────────────────────────

    private async Task<string> ExchangeCodeForTokenAsync(
        string tokenUrl,
        Dictionary<string, string> payload,
        CancellationToken ct)
    {
        using var client = _httpClientFactory.CreateClient("oauth");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        var resp = await client.PostAsync(
            tokenUrl,
            new FormUrlEncodedContent(payload),
            ct);

        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        // Check for OAuth error response
        if (root.TryGetProperty("error", out var errorProp))
        {
            string error = errorProp.GetString() ?? "unknown_error";
            string desc  = root.TryGetProperty("error_description", out var descProp)
                ? descProp.GetString() ?? string.Empty
                : string.Empty;
            _logger.LogWarning("OAuth token exchange failed: {Error} — {Desc}", error, desc);
            throw new UnauthorizedAccessException(
                $"OAuth authorization failed: {error}. {desc}".Trim());
        }

        string? token = root.TryGetProperty("access_token", out var tokenProp)
            ? tokenProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("OAuth provider did not return an access token.");

        return token;
    }

    // ── User upsert ───────────────────────────────────────────────────────────

    private async Task<(User user, bool isNewUser)> UpsertUserAsync(
        OAuthUserProfile profile,
        string           provider,
        string?          ipAddress,
        CancellationToken ct)
    {
        // 1. Look up by (provider, providerId) — most reliable; handles email changes
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u =>
                u.OAuthProvider   == provider &&
                u.OAuthProviderId == profile.ProviderId, ct);

        if (user is not null)
            return (user, false);   // returning OAuth user

        // 2. Look up by email — link existing local account to OAuth
        user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Email == profile.Email.ToLowerInvariant(), ct);

        if (user is not null)
        {
            // Existing local account found — link this OAuth provider to it
            if (string.IsNullOrEmpty(user.OAuthProvider))
            {
                user.LinkOAuthProvider(provider, profile.ProviderId);
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Linked {Provider} OAuth to existing account. UserId={UserId}",
                    provider, user.Id);
            }
            return (user, false);
        }

        // 3. Brand-new user — create OAuth account
        user = User.CreateOAuth(
            fullName:       profile.FullName,
            email:          profile.Email,
            oauthProvider:  provider,
            oauthProviderId: profile.ProviderId,
            profilePicUrl:  profile.ProfilePicUrl,
            role:           "Patient");

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "New OAuth user created. Provider={Provider} UserId={UserId} Email={Email}",
            provider, user.Id, user.Email);

        return (user, true);
    }

    // ── Token issuance ────────────────────────────────────────────────────────

    private async Task<(string accessToken, string refreshTokenValue, DateTime expiry)>
        IssueTokensAsync(User user, string? ipAddress, CancellationToken ct)
    {
        if (!user.IsActive)
            throw new UnauthorizedAccessException("This account has been deactivated.");

        // Re-attach with refresh tokens if not already loaded
        var trackedUser = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstAsync(u => u.Id == user.Id, ct);

        string accessToken = _jwtGenerator.GenerateAccessToken(trackedUser);
        var    expiry      = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes);

        var refreshToken = RefreshToken.Create(
            userId:     trackedUser.Id,
            expiryDays: _jwtSettings.RefreshTokenExpiryDays,
            ipAddress:  ipAddress);

        trackedUser.AddRefreshToken(refreshToken);
        await _db.SaveChangesAsync(ct);

        return (accessToken, refreshToken.Token, expiry);
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private string BuildCallbackUrl(string provider) =>
        $"{_oauthSettings.CallbackBaseUrl.TrimEnd('/')}/api/v1/auth/oauth/{provider}/callback";

    private static string BuildQueryString(Dictionary<string, string> parameters) =>
        string.Join("&", parameters.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

    private static UserDto MapToUserDto(User u) => new(
        u.Id, u.FullName, u.Email, u.Phone, u.Role, u.IsActive, u.CreatedAt,
        u.OAuthProvider, u.HasLocalPassword);
}

// ── Internal value type for provider profile data ─────────────────────────────

/// <summary>Normalised user profile data returned by any OAuth provider.</summary>
internal sealed record OAuthUserProfile(
    string  ProviderId,
    string  Email,
    string  FullName,
    string? ProfilePicUrl
);
