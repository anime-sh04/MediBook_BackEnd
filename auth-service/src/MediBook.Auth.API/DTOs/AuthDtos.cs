namespace MediBook.Auth.API.DTOs;

// ── UC-1: Patient Registration ────────────────────────────────────────────────

/// <summary>Payload for POST /api/v1/auth/register</summary>
public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string Phone
);

/// <summary>Returned on successful registration. No sensitive fields exposed.</summary>
public sealed record RegisterResponse(
    Guid     Id,
    string   FullName,
    string   Email,
    string   Phone,
    string   Role,
    bool     IsActive,
    DateTime CreatedAt
);

// ── UC-1p: Provider Registration ─────────────────────────────────────────────

/// <summary>Payload for POST /api/v1/auth/register-provider</summary>
public sealed record RegisterProviderRequest(
    string FullName,
    string Email,
    string Password,
    string Phone,
    string Specialization,
    string MedicalLicenseNumber
);

// ── UC-2: Login ───────────────────────────────────────────────────────────────

/// <summary>Payload for POST /api/v1/auth/login</summary>
public sealed record LoginRequest(
    string Email,
    string Password
);

/// <summary>
/// Returned on successful login (local or OAuth callback).
/// AccessToken  → short-lived JWT (1 hour).
/// RefreshToken → long-lived opaque string (7 days).
/// </summary>
public sealed record LoginResponse(
    string   AccessToken,
    string   RefreshToken,
    DateTime AccessTokenExpiry,
    UserDto  User
);

// ── OAuth ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Returned from GET /api/v1/auth/oauth/{provider}/callback after a successful
/// OAuth exchange. Identical shape to LoginResponse — the client treats it the same.
/// </summary>
public sealed record OAuthLoginResponse(
    string   AccessToken,
    string   RefreshToken,
    DateTime AccessTokenExpiry,
    UserDto  User,
    bool     IsNewUser   // true = account just created; false = existing account
);

// ── Token Refresh ─────────────────────────────────────────────────────────────

/// <summary>Payload for POST /api/v1/auth/refresh</summary>
public sealed record RefreshTokenRequest(
    string RefreshToken
);

/// <summary>Returned on successful token refresh.</summary>
public sealed record RefreshTokenResponse(
    string   AccessToken,
    string   RefreshToken,
    DateTime AccessTokenExpiry
);

// ── Logout ────────────────────────────────────────────────────────────────────

/// <summary>Payload for POST /api/v1/auth/logout</summary>
public sealed record LogoutRequest(
    string RefreshToken
);

// ── Profile Management ────────────────────────────────────────────────────────

/// <summary>Payload for PUT /api/v1/auth/profile</summary>
public sealed record UpdateProfileRequest(
    string  FullName,
    string  Phone,
    string? ProfilePicUrl
);

/// <summary>Payload for PUT /api/v1/auth/password</summary>
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

/// <summary>
/// Payload for PUT /api/v1/auth/set-password.
/// Used by OAuth users who want to add a local password to their account.
/// </summary>
public sealed record SetPasswordRequest(
    string NewPassword
);

// ── User Projection ───────────────────────────────────────────────────────────

/// <summary>Safe user projection — no password hash, no refresh tokens.</summary>
public sealed record UserDto(
    Guid     Id,
    string   FullName,
    string   Email,
    string   Phone,
    string   Role,
    bool     IsActive,
    DateTime CreatedAt,
    string?  OAuthProvider = null,
    bool     HasLocalPassword = true
);

// ── Shared Error Envelope ─────────────────────────────────────────────────────

/// <summary>Standard API error response used across all endpoints.</summary>
public sealed record ApiErrorResponse(
    string                   Message,
    IEnumerable<string>?     Errors = null
);
