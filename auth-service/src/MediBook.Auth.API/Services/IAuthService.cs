using MediBook.Auth.API.DTOs;

namespace MediBook.Auth.API.Services;

/// <summary>
/// Business contract for the Auth service.
/// UC-1:  RegisterAsync (Patient), RegisterProviderAsync (Provider)
/// UC-2:  LoginAsync, GetCurrentUserAsync, RefreshTokenAsync, LogoutAsync
/// UC-3:  UpdateProfileAsync, ChangePasswordAsync, DeactivateAccountAsync
/// OAuth: SetPasswordAsync (OAuth users adding a local password)
/// Util:  GetUserByEmailAsync, GetUserByIdAsync
/// </summary>
public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<RegisterResponse> RegisterProviderAsync(RegisterProviderRequest request, CancellationToken ct = default);

    Task<LoginResponse>          LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default);
    Task<UserDto>                GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
    Task<UserDto?>               GetUserByEmailAsync(string email, CancellationToken ct = default);
    Task<UserDto?>               GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<RefreshTokenResponse>   RefreshTokenAsync(string refreshToken, string? ipAddress, CancellationToken ct = default);
    Task                         LogoutAsync(string refreshToken, CancellationToken ct = default);

    Task<UserDto>  UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);
    Task           ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task           DeactivateAccountAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Allows an OAuth-only user to set a local password for the first time.
    /// After this, the user can also log in with email + password.
    /// </summary>
    /// <exception cref="KeyNotFoundException">User not found.</exception>
    /// <exception cref="InvalidOperationException">User already has a local password.</exception>
    Task SetPasswordAsync(Guid userId, SetPasswordRequest request, CancellationToken ct = default);
}
