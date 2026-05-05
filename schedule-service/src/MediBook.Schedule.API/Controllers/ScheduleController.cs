using MediBook.Schedule.API.DTOs;
using MediBook.Schedule.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediBook.Schedule.API.Controllers;

/// <summary>
/// Exposes /api/v1/slots endpoints matching the ScheduleController class diagram.
/// POST  (add / bulk / generateRecurring)
/// GET   (by provider / available / id)
/// PUT   (block / unblock / update)
/// DELETE
/// </summary>
[ApiController]
[Route("api/v1/slots")]
[Produces("application/json")]
public sealed class ScheduleController : ControllerBase
{
    private readonly IScheduleService          _schedService;
    private readonly ILogger<ScheduleController> _logger;

    public ScheduleController(IScheduleService schedService, ILogger<ScheduleController> logger)
    {
        _schedService = schedService;
        _logger       = logger;
    }

    // ── POST /api/v1/slots ────────────────────────────────────────────────────

    /// <summary>Add a single availability slot for a provider.</summary>
    /// <response code="201">Slot created.</response>
    /// <response code="400">Validation or argument error.</response>
    [HttpPost]
    [Authorize(Roles = "Provider,Admin")]
    [ProducesResponseType(typeof(AvailabilitySlotDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse),    StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddSlot([FromBody] AddSlotRequest request, CancellationToken ct)
    {
        try
        {
            var slot = await _schedService.AddSlotAsync(request, ct);
            return StatusCode(StatusCodes.Status201Created, slot);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    // ── POST /api/v1/slots/bulk ───────────────────────────────────────────────

    /// <summary>Bulk-create multiple availability slots in one request.</summary>
    /// <response code="201">Slots created.</response>
    /// <response code="400">Validation or argument error.</response>
    [HttpPost("bulk")]
    [Authorize(Roles = "Provider,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<AvailabilitySlotDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse),                   StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddBulk([FromBody] AddBulkSlotsRequest request, CancellationToken ct)
    {
        if (request.Slots is null || request.Slots.Count == 0)
            return BadRequest(new ApiErrorResponse("At least one slot is required."));

        try
        {
            var slots = await _schedService.AddBulkSlotsAsync(request, ct);
            return StatusCode(StatusCodes.Status201Created, slots);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    // ── POST /api/v1/slots/generate-recurring ─────────────────────────────────

    /// <summary>
    /// Generate recurring slots for a provider.
    /// Recurrence must be "daily" or "weekly".
    /// </summary>
    /// <response code="201">Recurring slots generated.</response>
    /// <response code="400">Invalid recurrence pattern or date range.</response>
    [HttpPost("generate-recurring")]
    [Authorize(Roles = "Provider,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<AvailabilitySlotDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse),                   StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateRecurring(
        [FromBody] GenerateRecurringRequest request, CancellationToken ct)
    {
        try
        {
            var slots = await _schedService.GenerateRecurringSlotsAsync(request, ct);
            return StatusCode(StatusCodes.Status201Created, slots);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }

    // ── GET /api/v1/slots/provider/{providerId} ───────────────────────────────

    /// <summary>Returns all slots (any state) for the specified provider.</summary>
    /// <response code="200">List of slots.</response>
    [HttpGet("provider/{providerId:Guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<AvailabilitySlotDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProvider(Guid providerId, CancellationToken ct)
    {
        var slots = await _schedService.GetSlotsByProviderAsync(providerId, ct);
        return Ok(slots);
    }

    // ── GET /api/v1/slots/available?providerId=&date= ─────────────────────────

    /// <summary>
    /// Returns unbooked, unblocked slots for a provider on a specific date.
    /// Exposed to patients — no auth required.
    /// </summary>
    /// <response code="200">List of available slots.</response>
    /// <response code="400">Missing or invalid parameters.</response>
    [HttpGet("available")]
    [ProducesResponseType(typeof(IReadOnlyList<AvailabilitySlotDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),                   StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAvailable(
        [FromQuery] Guid     providerId,
        [FromQuery] string? date,
        CancellationToken   ct)
    {
        if (string.IsNullOrWhiteSpace(date) || !DateOnly.TryParse(date, out var parsedDate))
            return BadRequest(new ApiErrorResponse("A valid 'date' query parameter (yyyy-MM-dd) is required."));

        var slots = await _schedService.GetAvailableSlotsAsync(providerId, parsedDate, ct);
        return Ok(slots);
    }

    // ── GET /api/v1/slots/{id} ────────────────────────────────────────────────

    /// <summary>Returns a single slot by SlotId.</summary>
    /// <response code="200">Slot details.</response>
    /// <response code="404">Slot not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AvailabilitySlotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),    StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        try
        {
            var slot = await _schedService.GetSlotByIdAsync(id, ct);
            return Ok(slot);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    // ── PUT /api/v1/slots/{id} ────────────────────────────────────────────────

    /// <summary>Updates an existing slot's date and time. Only allowed when not booked or blocked.</summary>
    /// <response code="200">Updated slot.</response>
    /// <response code="400">Argument error.</response>
    /// <response code="404">Slot not found.</response>
    /// <response code="409">Slot is booked or blocked.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Provider,Admin")]
    [ProducesResponseType(typeof(AvailabilitySlotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),    StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse),    StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse),    StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSlot(
        int id, [FromBody] UpdateSlotRequest request, CancellationToken ct)
    {
        try
        {
            var slot = await _schedService.UpdateSlotAsync(id, request, ct);
            return Ok(slot);
        }
        catch (KeyNotFoundException ex)      { return NotFound(new ApiErrorResponse(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(new ApiErrorResponse(ex.Message)); }
        catch (ArgumentException ex)         { return BadRequest(new ApiErrorResponse(ex.Message)); }
    }

    // ── PUT /api/v1/slots/{id}/block ──────────────────────────────────────────

    /// <summary>Blocks the slot (e.g. for provider leave). Releases any active booking.</summary>
    /// <response code="204">Slot blocked.</response>
    /// <response code="404">Slot not found.</response>
    [HttpPut("{id:int}/block")]
    [Authorize(Roles = "Provider,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BlockSlot(int id, CancellationToken ct)
    {
        try
        {
            await _schedService.BlockSlotAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    // ── PUT /api/v1/slots/{id}/unblock ────────────────────────────────────────

    /// <summary>Removes the block from a slot, making it available again.</summary>
    /// <response code="204">Slot unblocked.</response>
    /// <response code="404">Slot not found.</response>
    [HttpPut("{id:int}/unblock")]
    [Authorize(Roles = "Provider,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnblockSlot(int id, CancellationToken ct)
    {
        try
        {
            await _schedService.UnblockSlotAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    // ── PUT /api/v1/slots/{id}/book ───────────────────────────────────────────
    //
    // ★ SAGA ENTRY POINT
    // This endpoint is the trigger that starts the entire booking Saga.
    // It does NOT immediately mark the slot as booked; instead it:
    //   1. Validates the slot is available
    //   2. Publishes a PaymentRequested event to RabbitMQ
    //   3. Returns 202 Accepted with a CorrelationId
    //
    // The final slot state (CONFIRMED or AVAILABLE) is set asynchronously
    // by the PaymentResultConsumer background service.

    /// <summary>
    /// Initiates the slot-booking Saga via payment.
    /// Publishes PaymentRequested → Payment Service processes payment →
    /// PaymentSucceeded / PaymentFailed flows back to confirm or rollback.
    /// </summary>
    /// <response code="202">Saga initiated. Slot is PENDING payment confirmation.</response>
    /// <response code="400">Invalid request body.</response>
    /// <response code="404">Slot not found.</response>
    /// <response code="409">Slot is already booked or blocked.</response>
    [HttpPut("{id:int}/book")]
    [ProducesResponseType(typeof(BookSlotResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BookSlot(
        int id, [FromBody] BookSlotRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse("Request body is required."));

        try
        {
            // ★ Saga starts here — publishes PaymentRequested
            var correlationId = await _schedService.InitiateBookingAsync(id, request, ct);

            _logger.LogInformation(
                "Booking Saga initiated for SlotId={SlotId}, CorrelationId={CorrelationId}",
                id, correlationId);

            return Accepted(new BookSlotResponse(id, correlationId, "PENDING"));
        }
        catch (KeyNotFoundException ex)      { return NotFound(new ApiErrorResponse(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(new ApiErrorResponse(ex.Message)); }
        catch (ArgumentException ex)         { return BadRequest(new ApiErrorResponse(ex.Message)); }
    }

    // ── PUT /api/v1/slots/{id}/unbook ─────────────────────────────────────────

    /// <summary>
    /// Releases a booked slot back to available (Booked → Available).
    /// Called by the appointment-service on cancellation.
    /// </summary>
    /// <response code="204">Slot released.</response>
    /// <response code="404">Slot not found.</response>
    /// <response code="409">Slot is not currently booked.</response>
    [HttpPut("{id:int}/unbook")]
    [Authorize(Roles = "Patient,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UnbookSlot(int id, CancellationToken ct)
    {
        try
        {
            await _schedService.UnbookSlotAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)      { return NotFound(new ApiErrorResponse(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(new ApiErrorResponse(ex.Message)); }
    }

    // ── DELETE /api/v1/slots/{id} ─────────────────────────────────────────────

    /// <summary>Hard-deletes a slot.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="404">Slot not found.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Provider,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSlot(int id, CancellationToken ct)
    {
        try
        {
            await _schedService.DeleteSlotAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
    }

    // ── GET /api/v1/slots/health ──────────────────────────────────────────────

    /// <summary>Health check — no auth required.</summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() =>
        Ok(new { service = "MediBook.Schedule", status = "healthy", timestamp = DateTime.UtcNow });
}
