using System.Security.Claims;
using FluentValidation;
using MediBook.Auth.API.DTOs;
using MediBook.Auth.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediBook.Auth.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService               _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest>    _loginValidator;
    private readonly ILogger<AuthController>    _logger;

    public AuthController(
        IAuthService                authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest>    loginValidator,
        ILogger<AuthController>     logger)
    {
        _authService               = authService;
        _registerValidator         = registerValidator;
        _loginValidator            = loginValidator;
        _logger                    = logger;
    }

    // ── UC-1: Register ────────────────────────────────────────────────────────

    /// <summary>Register a new Patient account.</summary>
    /// <response code="201">Patient registered successfully.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="409">Email already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse),  StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse),  StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse),  StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        var validation = await _registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiErrorResponse("Validation failed.",
                validation.Errors.Select(e => e.ErrorMessage)));

        try
        {
            var response = await _authService.RegisterAsync(request, ct);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiErrorResponse(ex.Message));
        }
    }

    // ── UC-2: Login ───────────────────────────────────────────────────────────

    /// <summary>
    /// Login with email and password.
    /// Returns a short-lived JWT access token and a long-lived refresh token.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/auth/login
    ///     {
    ///         "email":    "rahul@example.com",
    ///         "password": "Secure@123"
    ///     }
    ///
    /// Use the returned **accessToken** as a Bearer token on all protected endpoints:
    ///
    ///     Authorization: Bearer {accessToken}
    ///
    /// When the access token expires (1 hour), use **POST /auth/refresh** with the refreshToken.
    /// </remarks>
    /// <response code="200">Login successful — tokens returned.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="401">Invalid credentials or inactive account.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse),    StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiErrorResponse("Validation failed.",
                validation.Errors.Select(e => e.ErrorMessage)));

        try
        {
            string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var response = await _authService.LoginAsync(request, ipAddress, ct);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiErrorResponse(ex.Message));
        }
    }

    // ── UC-2: Get Current User (protected endpoint) ───────────────────────────

    /// <summary>
    /// Returns the profile of the currently authenticated user.
    /// Requires a valid Bearer token in the Authorization header.
    /// </summary>
    /// <response code="200">User profile returned.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto),          StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        // The JWT middleware validates the token and populates User.Claims.
        // We extract the user's ID from the "sub" claim.
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue("sub");

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new ApiErrorResponse("Invalid token claims."));

        try
        {
            var userDto = await _authService.GetCurrentUserAsync(userId, ct);
            return Ok(userDto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpGet("users/{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken ct)
    {
        try
        {
            var userDto = await _authService.GetCurrentUserAsync(id, ct);
            return Ok(userDto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
    // ── UC-2: Refresh Token ───────────────────────────────────────────────────

    /// <summary>
    /// Exchange a valid refresh token for a new access token + refresh token pair.
    /// The old refresh token is revoked immediately (token rotation).
    /// </summary>
    /// <response code="200">New token pair issued.</response>
    /// <response code="401">Refresh token invalid, expired, or revoked.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),     StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new ApiErrorResponse("Refresh token is required."));

        try
        {
            string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var response = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress, ct);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiErrorResponse(ex.Message));
        }
    }

    // ── UC-2: Logout ──────────────────────────────────────────────────────────

    /// <summary>
    /// Revokes the refresh token, logging the user out.
    /// The access token remains valid until it naturally expires (1 hour).
    /// Clients should discard both tokens on logout.
    /// </summary>
    /// <response code="204">Logged out successfully.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken ct)
    {
        await _authService.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }

    // ── UC-1p: Register Provider ──────────────────────────────────────────────

    /// <summary>Register a new Provider account.</summary>
    /// <response code="201">Provider registered successfully.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="409">Email already registered.</response>
    [HttpPost("register-provider")]
    [ProducesResponseType(typeof(RegisterResponse),  StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse),  StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse),  StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterProvider(
        [FromBody] RegisterProviderRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Phone))
        {
            return BadRequest(new ApiErrorResponse("FullName, Email, Password and Phone are required."));
        }

        try
        {
            var response = await _authService.RegisterProviderAsync(request, ct);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiErrorResponse(ex.Message));
        }
    }

    // ── UC-3: Update Profile ──────────────────────────────────────────────────

    /// <summary>Update the authenticated user's name, phone, or profile picture.</summary>
    /// <response code="200">Profile updated.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="404">User not found.</response>
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto),          StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new ApiErrorResponse("Invalid token claims."));

        try
        {
            var dto = await _authService.UpdateProfileAsync(userId, request, ct);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    // ── UC-3: Change Password ─────────────────────────────────────────────────

    /// <summary>Change the authenticated user's password.</summary>
    /// <response code="204">Password changed successfully.</response>
    /// <response code="401">Invalid token or wrong current password.</response>
    /// <response code="404">User not found.</response>
    [HttpPut("password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new ApiErrorResponse("Invalid token claims."));

        try
        {
            await _authService.ChangePasswordAsync(userId, request, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiErrorResponse(ex.Message));
        }
    }

    // ── UC-3: Deactivate Account ──────────────────────────────────────────────

    /// <summary>
    /// Soft-deactivates the authenticated user's account.
    /// All active sessions are revoked immediately.
    /// </summary>
    /// <response code="204">Account deactivated.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="404">User not found.</response>
    [HttpDelete("deactivate")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new ApiErrorResponse("Invalid token claims."));

        try
        {
            await _authService.DeactivateAccountAsync(userId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    // ── Health Check ──────────────────────────────────────────────────────────

    /// <summary>Service health check — no auth required.</summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() =>
        Ok(new { service = "MediBook.Auth", status = "healthy", timestamp = DateTime.UtcNow });

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("sub");
        return claim is not null && Guid.TryParse(claim, out userId);
    }
}

