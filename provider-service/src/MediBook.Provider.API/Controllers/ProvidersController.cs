using System.Security.Claims;
using FluentValidation;
using MediBook.Provider.API.DTOs;
using MediBook.Provider.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediBook.Provider.API.Controllers;

[ApiController]
[Route("api/v1/providers")]
[Produces("application/json")]
public sealed class ProvidersController : ControllerBase
{
    private readonly IProviderService                    _providerService;
    private readonly IValidator<RegisterProviderRequest> _validator;
    private readonly ILogger<ProvidersController>        _logger;

    public ProvidersController(
        IProviderService                    providerService,
        IValidator<RegisterProviderRequest> validator,
        ILogger<ProvidersController>        logger)
    {
        _providerService = providerService;
        _validator       = validator;
        _logger          = logger;
    }

    // ── POST /api/v1/providers/register ──────────────────────────────────────
    // Requires the user to already have a Provider-role JWT (issued by auth-service
    // after POST /auth/register-provider).

    /// <summary>
    /// Create a provider profile for the authenticated Provider-role user.
    /// Requires role: Provider.
    /// </summary>
    /// <response code="201">Profile created.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">User is not a Provider.</response>
    /// <response code="409">Profile already exists for this user.</response>
    [HttpPost("register")]
    [Authorize(Roles = "Provider")]
    [ProducesResponseType(typeof(ProviderProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse),   StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse),   StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterProvider(
        [FromBody] RegisterProviderRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiErrorResponse("Validation failed.",
                validation.Errors.Select(e => e.ErrorMessage)));

        if (!TryGetUserId(out var userId))
            return Unauthorized(new ApiErrorResponse("Invalid user token."));

        try
        {
            var profile = await _providerService.RegisterProviderAsync(userId, request, ct);
            return StatusCode(StatusCodes.Status201Created, profile);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiErrorResponse(ex.Message));
        }
    }

    // ── GET /api/v1/providers ─────────────────────────────────────────────────

    /// <summary>
    /// Browse all verified providers with optional filters.
    /// Publicly accessible (no auth required).
    /// </summary>
    /// <response code="200">Paged list of providers.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProviderProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string?  specialization = null,
        [FromQuery] string?  city           = null,
        [FromQuery] bool?    isAvailable    = null,
        [FromQuery] int      page           = 1,
        [FromQuery] int      pageSize       = 20,
        CancellationToken ct = default)
    {
        var query = new ProviderSearchQuery(
            Specialization: specialization,
            City:           city,
            IsAvailable:    isAvailable,
            Page:           Math.Max(1, page),
            PageSize:       Math.Clamp(pageSize, 1, 100));

        var result = await _providerService.GetAllProvidersAsync(query, ct);
        return Ok(result);
    }

    // ── GET /api/v1/providers/search ──────────────────────────────────────────

    /// <summary>
    /// Full-text search across specialization, clinic name, city, and address.
    /// Publicly accessible.
    /// </summary>
    /// <response code="200">List of matching providers.</response>
    /// <response code="400">Search term is required.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<ProviderProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),                  StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new ApiErrorResponse("Search term 'q' is required."));

        var results = await _providerService.SearchProvidersAsync(q, ct);
        return Ok(results);
    }

    // ── GET /api/v1/providers/specialization/{specialization} ─────────────────

    /// <summary>
    /// Returns all verified providers whose specialization matches the given term.
    /// Publicly accessible.
    /// </summary>
    /// <response code="200">List of providers.</response>
    [HttpGet("specialization/{specialization}")]
    [ProducesResponseType(typeof(IReadOnlyList<ProviderProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBySpecialization(
        string specialization, CancellationToken ct)
    {
        var results = await _providerService.GetBySpecializationAsync(specialization, ct);
        return Ok(results);
    }

    // ── GET /api/v1/providers/{id} ────────────────────────────────────────────

    /// <summary>
    /// Returns a single provider profile by ProviderId.
    /// Publicly accessible.
    /// </summary>
    /// <response code="200">Provider profile.</response>
    /// <response code="404">Not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProviderProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),   StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var profile = await _providerService.GetProviderByIdAsync(id, ct);
            return Ok(profile);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    // ── GET /api/v1/providers/me ──────────────────────────────────────────────

    /// <summary>
    /// Returns the provider profile of the currently authenticated Provider.
    /// Requires role: Provider.
    /// </summary>
    /// <response code="200">Provider profile.</response>
    /// <response code="404">Profile not yet created.</response>
    [HttpGet("me")]
    [Authorize(Roles = "Provider")]
    [ProducesResponseType(typeof(ProviderProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),   StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new ApiErrorResponse("Invalid user token."));

        try
        {
            var profile = await _providerService.GetMyProfileAsync(userId, ct);
            return Ok(profile);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    // ── PUT /api/v1/providers/me ──────────────────────────────────────────────

    /// <summary>
    /// Updates the provider profile of the currently authenticated Provider.
    /// Requires role: Provider.
    /// </summary>
    /// <response code="200">Updated profile.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="404">Profile not found.</response>
    [HttpPut("me")]
    [Authorize(Roles = "Provider")]
    [ProducesResponseType(typeof(ProviderProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),   StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse),   StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] RegisterProviderRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiErrorResponse("Validation failed.",
                validation.Errors.Select(e => e.ErrorMessage)));

        if (!TryGetUserId(out var userId))
            return Unauthorized(new ApiErrorResponse("Invalid user token."));

        try
        {
            var profile = await _providerService.UpdateMyProfileAsync(userId, request, ct);
            return Ok(profile);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    // ── PUT /api/v1/providers/{id}/availability ───────────────────────────────

    /// <summary>
    /// Set the availability flag for a provider.
    /// Providers can toggle their own; Admins can toggle any.
    /// </summary>
    /// <response code="200">Updated profile.</response>
    /// <response code="403">Not allowed to change this provider's availability.</response>
    /// <response code="404">Not found.</response>
    [HttpPut("{id:guid}/availability")]
    [Authorize(Roles = "Provider,Admin")]
    [ProducesResponseType(typeof(ProviderProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),   StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse),   StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAvailability(
        Guid id, [FromBody] SetAvailabilityRequest request, CancellationToken ct)
    {
        // Providers may only update their own availability
        bool isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new ApiErrorResponse("Invalid user token."));

            // Verify the provider profile belongs to this user
            try
            {
                var myProfile = await _providerService.GetMyProfileAsync(userId, ct);
                if (myProfile.ProviderId != id)
                    return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return Forbid();
            }
        }

        try
        {
            var updated = await _providerService.SetAvailabilityAsync(id, request.IsAvailable, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    // ── PUT /api/v1/providers/{id}/verify ─────────────────────────────────────

    /// <summary>
    /// Verify or reject a provider's credentials.
    /// Requires role: Admin.
    /// </summary>
    /// <response code="200">Updated profile.</response>
    /// <response code="404">Not found.</response>
    [HttpPut("{id:guid}/verify")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ProviderProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),   StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyProvider(
        Guid id, [FromQuery] bool isVerified = true, CancellationToken ct = default)
    {
        try
        {
            var updated = await _providerService.VerifyProviderAsync(id, isVerified, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    // ── PUT /api/v1/providers/{id}/rating ─────────────────────────────────────

    /// <summary>
    /// Internal endpoint called by the review-service to sync average rating.
    /// Requires role: Admin (service-to-service calls use an Admin-role service token).
    /// </summary>
    /// <response code="204">Rating updated.</response>
    /// <response code="404">Not found.</response>
    [HttpPut("{id:guid}/rating")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRating(
        Guid id, [FromBody] UpdateRatingRequest request, CancellationToken ct)
    {
        try
        {
            await _providerService.UpdateRatingAsync(id, request.NewAvgRating, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    // ── DELETE /api/v1/providers/{id} ─────────────────────────────────────────

    /// <summary>
    /// Hard-delete a provider profile.
    /// Requires role: Admin.
    /// </summary>
    /// <response code="204">Deleted.</response>
    /// <response code="404">Not found.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProvider(Guid id, CancellationToken ct)
    {
        try
        {
            await _providerService.DeleteProviderAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    // ── GET /api/v1/providers/health ──────────────────────────────────────────

    /// <summary>Service health check — no auth required.</summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() =>
        Ok(new { service = "MediBook.Provider", status = "healthy", timestamp = DateTime.UtcNow });

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("sub");
        return claim is not null && Guid.TryParse(claim, out userId);
    }
}
