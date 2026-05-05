using System.Security.Claims;
using MediBook.Auth.API.DTOs;
using MediBook.Auth.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediBook.Auth.API.Controllers;

/// <summary>
/// Handles the OAuth2 authorization-code flow for Google and GitHub.
/// Supported providers (pass in the URL path):
///   • google
///   • github
/// </summary>
[ApiController]
[Route("api/v1/auth/oauth")]
[Produces("application/json")]
public sealed class OAuthController : ControllerBase
{
    private readonly IOAuthService          _oauthService;
    private readonly IAuthService           _authService;
    private readonly ILogger<OAuthController> _logger;

    public OAuthController(
        IOAuthService            oauthService,
        IAuthService             authService,
        ILogger<OAuthController> logger)
    {
        _oauthService = oauthService;
        _authService  = authService;
        _logger       = logger;
    }

    // ── Step 1: Initiate OAuth login ──────────────────────────────────────────

    /// <summary>
    /// Initiates the OAuth2 login flow for the given provider.
    /// Returns HTTP 302 to redirect the user's browser to the provider's
    /// authorization/consent screen.
    /// </summary>
    /// <param name="provider">OAuth provider name: <c>google</c> or <c>github</c></param>
    [HttpGet("{provider}/login")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(string provider, CancellationToken ct)
    {
        try
        {
            string? ip  = HttpContext.Connection.RemoteIpAddress?.ToString();
            string  url = await _oauthService.GetAuthorizationUrlAsync(provider, ip, ct);
            return Redirect(url);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    // ── Step 2: OAuth callback ────────────────────────────────────────────────

    /// <summary>
    /// Handles the OAuth2 callback from the provider after the user
    /// grants (or denies) consent.
    /// On success, redirects to the Angular frontend with tokens in query params.
    /// </summary>
    /// <param name="provider">OAuth provider name: <c>google</c> or <c>github</c></param>
    [HttpGet("{provider}/callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Callback(
        string  provider,
        [FromQuery] string? code  = null,
        [FromQuery] string? state = null,
        [FromQuery] string? error = null,
        CancellationToken   ct    = default)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            _logger.LogInformation(
                "OAuth consent denied. Provider={Provider} Error={Error}", provider, error);
            return Redirect($"https://red-bay-0178e2c00.7.azurestaticapps.net/login?error=oauth_denied");
        }

        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new ApiErrorResponse("Authorization code is missing."));

        if (string.IsNullOrWhiteSpace(state))
            return BadRequest(new ApiErrorResponse("State parameter is missing."));

        try
        {
            string? ip   = HttpContext.Connection.RemoteIpAddress?.ToString();
            var response = await _oauthService.HandleCallbackAsync(provider, code, state, ip, ct);

            var redirectUrl =
                $"https://red-bay-0178e2c00.7.azurestaticapps.net/auth/oauth-success" +
                $"?accessToken={Uri.EscapeDataString(response.AccessToken)}" +
                $"&refreshToken={Uri.EscapeDataString(response.RefreshToken)}";

            return Redirect(redirectUrl);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiErrorResponse(ex.Message));
        }
    }

    // ── Set local password (OAuth users only) ─────────────────────────────────

    /// <summary>
    /// Allows a user who registered via OAuth (Google/GitHub) to set a local
    /// email + password on their account.
    /// </summary>
    [HttpPut("set-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetPassword(
        [FromBody] SetPasswordRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new ApiErrorResponse(
                "Password must be at least 8 characters."));

        if (!TryGetUserId(out var userId))
            return Unauthorized(new ApiErrorResponse("Invalid token claims."));

        try
        {
            await _authService.SetPasswordAsync(userId, request, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("sub");
        return claim is not null && Guid.TryParse(claim, out userId);
    }
}