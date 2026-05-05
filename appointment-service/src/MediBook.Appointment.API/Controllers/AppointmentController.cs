using MediBook.Appointment.API.DTOs;
using MediBook.Appointment.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediBook.Appointment.API.Controllers;

/// <summary>
/// Exposes /api/v1/appointments endpoints.
///
/// Saga design note:
///   POST /appointments has been REMOVED.
///   Appointments are now created exclusively via the PaymentSucceededConsumer
///   background service in response to a PaymentSucceeded RabbitMQ event.
///   This ensures the Appointment Service never initiates or double-triggers
///   the booking Saga.
///
/// Available endpoints:
///   GET   by id / patient / provider / provider+date / upcoming / count
///   PUT   cancel / reschedule / complete / status
/// </summary>
[ApiController]
[Route("api/v1/appointments")]
[Produces("application/json")]
public sealed class AppointmentController : ControllerBase
{
    private readonly IAppointmentService             _apptService;
    private readonly ILogger<AppointmentController>  _logger;

    public AppointmentController(
        IAppointmentService            apptService,
        ILogger<AppointmentController> logger)
    {
        _apptService = apptService;
        _logger      = logger;
    }

    // ── GET /api/v1/appointments/{id} ─────────────────────────────────────────

    /// <summary>Get a single appointment by its ID.</summary>
    /// <response code="200">Appointment details.</response>
    /// <response code="404">Not found.</response>
    [HttpGet("{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        try
        {
            var appt = await _apptService.GetByIdAsync(id, ct);
            return Ok(appt);
        }
        catch (KeyNotFoundException ex) { return NotFound(new ApiErrorResponse(ex.Message)); }
    }

    // ── GET /api/v1/appointments/patient/{patientId} ──────────────────────────

    /// <summary>Get all appointments for a patient.</summary>
    [HttpGet("patient/{patientId:guid}")]
    [Authorize(Roles = "Patient,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPatient(Guid patientId, CancellationToken ct)
        => Ok(await _apptService.GetByPatientAsync(patientId, ct));

    // ── GET /api/v1/appointments/provider/{providerId} ────────────────────────

    /// <summary>Get all appointments for a provider.</summary>
    [HttpGet("provider/{providerId:guid}")]
    [Authorize(Roles = "Provider,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProvider(Guid providerId, CancellationToken ct)
        => Ok(await _apptService.GetByProviderAsync(providerId, ct));

    // ── GET /api/v1/appointments/provider/{providerId}/date/{date} ────────────

    /// <summary>Get appointments for a provider on a specific date.</summary>
    [HttpGet("provider/{providerId:guid}/date/{date}")]
    [Authorize(Roles = "Provider,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByProviderAndDate(
        Guid providerId, string date, CancellationToken ct)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
            return BadRequest(new ApiErrorResponse("Date must be in yyyy-MM-dd format."));

        var appts = await _apptService.GetByProviderAndDateAsync(providerId, parsedDate, ct);
        return Ok(appts);
    }

    // ── GET /api/v1/appointments/patient/{patientId}/upcoming ─────────────────

    /// <summary>Get upcoming (Scheduled, future) appointments for a patient.</summary>
    [HttpGet("patient/{patientId:guid}/upcoming")]
    [Authorize(Roles = "Patient,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcoming(Guid patientId, CancellationToken ct)
        => Ok(await _apptService.GetUpcomingByPatientAsync(patientId, ct));

    // ── PUT /api/v1/appointments/{id}/cancel ──────────────────────────────────

    /// <summary>
    /// Cancel an appointment.
    /// Releases the slot in schedule-service and triggers a refund (stubbed).
    /// </summary>
    /// <response code="204">Cancelled.</response>
    /// <response code="404">Not found.</response>
    /// <response code="409">Appointment is not in Scheduled state.</response>
    [HttpPut("{id:int}/cancel")]
    [Authorize(Roles = "Patient,Provider,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        try
        {
            await _apptService.CancelAppointmentAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)      { return NotFound(new ApiErrorResponse(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(new ApiErrorResponse(ex.Message)); }
    }

    // ── PUT /api/v1/appointments/{id}/reschedule ──────────────────────────────

    /// <summary>
    /// Reschedule an appointment to a new slot.
    /// Releases the old slot and validates the new slot via Schedule Service.
    /// </summary>
    /// <response code="200">Updated appointment.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="404">Appointment or new slot not found.</response>
    /// <response code="409">New slot unavailable or appointment not Scheduled.</response>
    [HttpPut("{id:int}/reschedule")]
    [Authorize(Roles = "Patient,Admin")]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reschedule(
        int id, [FromBody] RescheduleRequest request, CancellationToken ct)
    {
        try
        {
            var appt = await _apptService.RescheduleAppointmentAsync(id, request, ct);
            return Ok(appt);
        }
        catch (ArgumentException ex)         { return BadRequest(new ApiErrorResponse(ex.Message)); }
        catch (KeyNotFoundException ex)      { return NotFound(new ApiErrorResponse(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(new ApiErrorResponse(ex.Message)); }
    }

    // ── PUT /api/v1/appointments/{id}/complete ────────────────────────────────

    /// <summary>Mark a scheduled appointment as completed.</summary>
    /// <response code="204">Completed.</response>
    /// <response code="404">Not found.</response>
    /// <response code="409">Appointment not in Scheduled state.</response>
    [HttpPut("{id:int}/complete")]
    [Authorize(Roles = "Provider,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
    {
        try
        {
            await _apptService.CompleteAppointmentAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)      { return NotFound(new ApiErrorResponse(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(new ApiErrorResponse(ex.Message)); }
    }

    // ── PUT /api/v1/appointments/{id}/status ──────────────────────────────────

    /// <summary>
    /// Generic status override for admin use.
    /// Valid values: Scheduled, Completed, Cancelled, No-Show.
    /// </summary>
    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        int id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        try
        {
            var newStatus = await _apptService.UpdateStatusAsync(id, request.Status, ct);
            return Ok(new { status = newStatus });
        }
        catch (ArgumentException ex)    { return BadRequest(new ApiErrorResponse(ex.Message)); }
        catch (KeyNotFoundException ex) { return NotFound(new ApiErrorResponse(ex.Message)); }
    }

    // ── GET /api/v1/appointments/provider/{providerId}/count ──────────────────

    /// <summary>Returns the total appointment count for a provider.</summary>
    [HttpGet("provider/{providerId:guid}/count")]
    [Authorize(Roles = "Provider,Admin")]
    [ProducesResponseType(typeof(AppointmentCountDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCount(Guid providerId, CancellationToken ct)
    {
        var count = await _apptService.GetAppointmentCountAsync(providerId, ct);
        return Ok(new AppointmentCountDto(providerId, count));
    }

    // ── GET /api/v1/appointments/health ───────────────────────────────────────

    /// <summary>Service health check — no auth required.</summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() =>
        Ok(new { service = "MediBook.Appointment", status = "healthy", timestamp = DateTime.UtcNow });
}
