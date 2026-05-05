using MediBook.Review.API.DTOs;
using MediBook.Review.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediBook.Review.API.Controllers;

/// <summary>
/// Exposes /api/v1/reviews endpoints.
/// POST   add
/// GET    by provider / patient / appointment / all
/// PUT    update
/// DELETE remove (admin moderation or patient retraction)
/// GET    avgRating / count
/// </summary>
[ApiController]
[Route("api/v1/reviews")]
[Produces("application/json")]
public sealed class ReviewController : ControllerBase
{
    private readonly IReviewService             _reviewService;
    private readonly ILogger<ReviewController>  _logger;

    public ReviewController(
        IReviewService            reviewService,
        ILogger<ReviewController> logger)
    {
        _reviewService = reviewService;
        _logger        = logger;
    }

    // ── POST /api/v1/reviews ─────────────────────────────────────────────────

    /// <summary>Submit a new review for a completed appointment.</summary>
    /// <response code="201">Review created.</response>
    /// <response code="400">Validation or argument error.</response>
    /// <response code="409">A review already exists for this appointment.</response>
    [HttpPost]
    [Authorize(Roles = "Patient,Admin")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddReview(
        [FromBody] AddReviewRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await _reviewService.AddReviewAsync(request, ct);
            return StatusCode(StatusCodes.Status201Created, dto);
        }
        catch (ArgumentException ex)         { return BadRequest(new ApiErrorResponse(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(new ApiErrorResponse(ex.Message)); }
    }

    // ── GET /api/v1/reviews/provider/{providerId} ────────────────────────────

    /// <summary>Get all reviews for a specific provider.</summary>
    /// <response code="200">List of reviews.</response>
    [HttpGet("provider/{providerId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProvider(Guid providerId, CancellationToken ct)
    {
        var reviews = await _reviewService.GetByProviderAsync(providerId, ct);
        return Ok(reviews);
    }

    // ── GET /api/v1/reviews/patient/{patientId} ──────────────────────────────

    /// <summary>Get all reviews submitted by a specific patient.</summary>
    /// <response code="200">List of reviews.</response>
    [HttpGet("patient/{patientId:guid}")]
    [Authorize(Roles = "Patient,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPatient(Guid patientId, CancellationToken ct)
    {
        var reviews = await _reviewService.GetByPatientAsync(patientId, ct);
        return Ok(reviews);
    }

    // ── GET /api/v1/reviews/appointment/{appointmentId} ──────────────────────

    /// <summary>Get the review linked to a specific appointment (if any).</summary>
    /// <response code="200">Review details.</response>
    /// <response code="404">No review for this appointment.</response>
    [HttpGet("appointment/{appointmentId:int}")]
    [Authorize]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByAppointment(int appointmentId, CancellationToken ct)
    {
        var review = await _reviewService.GetByAppointmentAsync(appointmentId, ct);
        if (review is null)
            return NotFound(new ApiErrorResponse($"No review found for appointment {appointmentId}."));
        return Ok(review);
    }

    // ── GET /api/v1/reviews ──────────────────────────────────────────────────

    /// <summary>Get all reviews on the platform (admin view).</summary>
    /// <response code="200">All reviews.</response>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var reviews = await _reviewService.GetAllReviewsAsync(ct);
        return Ok(reviews);
    }

    // ── PUT /api/v1/reviews/{reviewId} ───────────────────────────────────────

    /// <summary>Update rating and comment of an existing review.</summary>
    /// <response code="200">Updated review.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="404">Review not found.</response>
    [HttpPut("{reviewId:int}")]
    [Authorize(Roles = "Patient,Admin")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReview(
        int reviewId, [FromBody] UpdateReviewRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await _reviewService.UpdateReviewAsync(reviewId, request, ct);
            return Ok(dto);
        }
        catch (ArgumentException ex)    { return BadRequest(new ApiErrorResponse(ex.Message)); }
        catch (KeyNotFoundException ex) { return NotFound(new ApiErrorResponse(ex.Message)); }
    }

    // ── DELETE /api/v1/reviews/{reviewId} ────────────────────────────────────

    /// <summary>Delete a review (admin moderation or patient retraction).</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="404">Review not found.</response>
    [HttpDelete("{reviewId:int}")]
    [Authorize(Roles = "Patient,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReview(int reviewId, CancellationToken ct)
    {
        try
        {
            await _reviewService.DeleteReviewAsync(reviewId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new ApiErrorResponse(ex.Message)); }
    }

    // ── GET /api/v1/reviews/provider/{providerId}/avg-rating ─────────────────

    /// <summary>Get average star rating and review count for a provider.</summary>
    /// <response code="200">Average rating with count.</response>
    [HttpGet("provider/{providerId:guid}/avg-rating")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AvgRatingDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvgRating(Guid providerId, CancellationToken ct)
    {
        var dto = await _reviewService.GetAvgRatingAsync(providerId, ct);
        return Ok(dto);
    }

    // ── GET /api/v1/reviews/provider/{providerId}/count ──────────────────────

    /// <summary>Get total review count for a provider.</summary>
    /// <response code="200">Review count.</response>
    [HttpGet("provider/{providerId:guid}/count")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCount(Guid providerId, CancellationToken ct)
    {
        var count = await _reviewService.GetReviewCountAsync(providerId, ct);
        return Ok(count);
    }
}
