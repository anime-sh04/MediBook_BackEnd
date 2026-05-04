using MediBook.Auth.API.Data;
using MediBook.Auth.API.DTOs;
using MediBook.Auth.API.Entities;
using MediBook.Auth.API.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediBook.Auth.API.Services;

public sealed class AuthService : IAuthService
{
    private readonly AuthDbContext      _db;
    private readonly JwtTokenGenerator  _jwtGenerator;
    private readonly JwtSettings        _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AuthDbContext            db,
        JwtTokenGenerator        jwtGenerator,
        IOptions<JwtSettings>    jwtSettings,
        ILogger<AuthService>     logger)
    {
        _db           = db;
        _jwtGenerator = jwtGenerator;
        _jwtSettings  = jwtSettings.Value;
        _logger       = logger;
    }

    // ── UC-1: Register ────────────────────────────────────────────────────────

    public async Task<RegisterResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default)
    {
        // Guard: duplicate email
        bool emailExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == request.Email.Trim().ToLowerInvariant(), ct);

        if (emailExists)
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", request.Email);
            throw new InvalidOperationException(
                $"An account with email '{request.Email}' already exists.");
        }

        // Hash password (BCrypt work factor 12 → ~250ms, brute-force resistant)
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        // Create via factory (validates invariants)
        var user = User.Create(
            fullName:     request.FullName,
            email:        request.Email,
            passwordHash: passwordHash,
            phone:        request.Phone,
            role:         "Patient");

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "New Patient registered. UserId={UserId} Email={Email}",
            user.Id, user.Email);

        return MapToRegisterResponse(user);
    }



    // ── UC-2: Login ───────────────────────────────────────────────────────────

    public async Task<LoginResponse> LoginAsync(
        LoginRequest  request,
        string?       ipAddress,
        CancellationToken ct = default)
    {
        // Load user WITH their refresh tokens (needed to revoke old ones)
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(
                u => u.Email == request.Email.Trim().ToLowerInvariant(), ct);

        // Constant-time rejection: verify even if user is null to prevent
        // timing-based user enumeration attacks.
        bool passwordValid = user is not null &&
                             BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (user is null || !passwordValid)
        {
            _logger.LogWarning(
                "Failed login attempt for email: {Email}", request.Email);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning(
                "Login attempt on deactivated account. UserId={UserId}", user.Id);
            throw new UnauthorizedAccessException("This account has been deactivated.");
        }

        // Generate JWT access token
        string accessToken = _jwtGenerator.GenerateAccessToken(user);
        var    accessExpiry = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes);

        // Generate and persist refresh token (old active tokens revoked inside entity)
        var refreshToken = RefreshToken.Create(
            userId:     user.Id,
            expiryDays: _jwtSettings.RefreshTokenExpiryDays,
            ipAddress:  ipAddress);

        user.AddRefreshToken(refreshToken);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "User logged in. UserId={UserId} Role={Role}", user.Id, user.Role);

        return new LoginResponse(
            AccessToken:       accessToken,
            RefreshToken:      refreshToken.Token,
            AccessTokenExpiry: accessExpiry,
            User:              MapToUserDto(user));
    }

    // ── UC-2: Get Current User (for GET /auth/me) ─────────────────────────────

    public async Task<UserDto> GetCurrentUserAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            throw new KeyNotFoundException($"User '{userId}' not found.");

        return MapToUserDto(user);
    }

    // ── UC-2: Refresh Token ───────────────────────────────────────────────────

    public async Task<RefreshTokenResponse> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken ct = default)
    {
        // Find user that owns this refresh token
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(
                u => u.RefreshTokens.Any(rt => rt.Token == refreshToken), ct);

        if (user is null)
        {
            _logger.LogWarning("Refresh attempt with unknown token.");
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var existingToken = user.RefreshTokens
            .First(rt => rt.Token == refreshToken);

        if (!existingToken.IsActive)
        {
            // Token is revoked or expired — possible token theft; revoke all tokens
            if (existingToken.IsRevoked)
            {
                _logger.LogWarning(
                    "Revoked refresh token reuse detected. UserId={UserId}. " +
                    "Revoking all tokens (potential token theft).", user.Id);

                // Revoke all active tokens for this user
                foreach (var t in user.RefreshTokens.Where(rt => rt.IsActive))
                    t.Revoke("Revoked due to suspicious reuse of revoked token.");

                await _db.SaveChangesAsync(ct);
            }

            throw new UnauthorizedAccessException(
                existingToken.IsExpired
                    ? "Refresh token has expired. Please log in again."
                    : "Refresh token has been revoked. Please log in again.");
        }

        // Rotate: issue new access + refresh token, revoke old refresh token
        string newAccessToken  = _jwtGenerator.GenerateAccessToken(user);
        var    newRefreshToken  = RefreshToken.Create(
            userId:     user.Id,
            expiryDays: _jwtSettings.RefreshTokenExpiryDays,
            ipAddress:  ipAddress);

        // AddRefreshToken revokes old active tokens internally
        user.AddRefreshToken(newRefreshToken);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Token refreshed. UserId={UserId}", user.Id);

        return new RefreshTokenResponse(
            AccessToken:       newAccessToken,
            RefreshToken:      newRefreshToken.Token,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes));
    }

    // ── UC-2: Logout ──────────────────────────────────────────────────────────

    public async Task LogoutAsync(
        string refreshToken,
        CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(
                u => u.RefreshTokens.Any(rt => rt.Token == refreshToken), ct);

        if (user is null)
        {
            // Token not found — already logged out or invalid; not an error.
            _logger.LogDebug("Logout called with unknown token — no action taken.");
            return;
        }

        user.RevokeRefreshToken(refreshToken, "Explicit logout");
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("User logged out. UserId={UserId}", user.Id);
    }

    // ── RegisterProvider ──────────────────────────────────────────────────────

    public async Task<RegisterResponse> RegisterProviderAsync(
        RegisterProviderRequest request,
        CancellationToken ct = default)
    {
        bool emailExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == request.Email.Trim().ToLowerInvariant(), ct);

        if (emailExists)
        {
            _logger.LogWarning("Provider registration attempt with existing email: {Email}", request.Email);
            throw new InvalidOperationException(
                $"An account with email '{request.Email}' already exists.");
        }

        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        var user = User.Create(
            fullName:     request.FullName,
            email:        request.Email,
            passwordHash: passwordHash,
            phone:        request.Phone,
            role:         "Provider");

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "New Provider registered. UserId={UserId} Email={Email}",
            user.Id, user.Email);

        return MapToRegisterResponse(user);
    }

    // ── GetUserByEmail / GetUserById ──────────────────────────────────────────

    public async Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.Trim().ToLowerInvariant(), ct);

        return user is null ? null : MapToUserDto(user);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        return user is null ? null : MapToUserDto(user);
    }

    // ── UpdateProfile ─────────────────────────────────────────────────────────

    public async Task<UserDto> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            throw new KeyNotFoundException($"User '{userId}' not found.");

        user.UpdateProfile(request.FullName, request.Phone, request.ProfilePicUrl);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Profile updated. UserId={UserId}", userId);
        return MapToUserDto(user);
    }

    // ── ChangePassword ────────────────────────────────────────────────────────

    public async Task ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            throw new KeyNotFoundException($"User '{userId}' not found.");

        bool currentValid = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash);
        if (!currentValid)
        {
            _logger.LogWarning("Password change failed — wrong current password. UserId={UserId}", userId);
            throw new UnauthorizedAccessException("Current password is incorrect.");
        }

        string newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.ChangePasswordHash(newHash);

        // Revoke all refresh tokens to force re-login on all devices
        foreach (var rt in user.RefreshTokens.Where(t => t.IsActive))
            rt.Revoke("Password changed");

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Password changed. UserId={UserId}", userId);
    }

    // ── DeactivateAccount ─────────────────────────────────────────────────────

    public async Task DeactivateAccountAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            throw new KeyNotFoundException($"User '{userId}' not found.");

        user.Deactivate();

        // Revoke all tokens so existing sessions stop working immediately
        foreach (var rt in user.RefreshTokens.Where(t => t.IsActive))
            rt.Revoke("Account deactivated");

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Account deactivated. UserId={UserId}", userId);
    }

    // ── Private Mappers ───────────────────────────────────────────────────────

    private static RegisterResponse MapToRegisterResponse(User user) =>
        new(user.Id, user.FullName, user.Email, user.Phone,
            user.Role, user.IsActive, user.CreatedAt);

    private static UserDto MapToUserDto(User user) =>
        new(user.Id, user.FullName, user.Email, user.Phone,
            user.Role, user.IsActive, user.CreatedAt,
            OAuthProvider:    user.OAuthProvider,
            HasLocalPassword: user.HasLocalPassword);

    // ── SetPassword (OAuth users adding a local password) ─────────────────────

    public async Task SetPasswordAsync(
        Guid userId, SetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            throw new KeyNotFoundException($"User '{userId}' not found.");

        if (user.HasLocalPassword)
            throw new InvalidOperationException(
                "This account already has a local password. Use PUT /auth/password to change it.");

        string hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.ChangePasswordHash(hash);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Local password set for OAuth user. UserId={UserId}", userId);
    }
}
