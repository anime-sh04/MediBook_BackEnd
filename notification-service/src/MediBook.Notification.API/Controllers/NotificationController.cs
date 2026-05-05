using FluentValidation;
using MediBook.Notification.API.DTOs;
using MediBook.Notification.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediBook.Notification.API.Controllers;

/// <summary>
/// REST API for the MediBook Notification Service.
///
/// Base URL: /api/v1/notifications
///
/// Endpoints:
///   POST   /send               — Send a single notification (in-app + email)
///   POST   /bulk               — Admin: broadcast to many recipients
///   POST   /email              — Send a raw email (no persisted record)
///   GET    /recipient/{id}     — Get notifications for a user (paginated)
///   GET    /unread/{id}        — Get unread count for a user
///   GET    /all                — Admin: get all notifications (paginated)
///   PUT    /{id}/read          — Mark single notification as read
///   PUT    /recipient/{id}/read-all — Mark all as read for a user
///   DELETE /{id}               — Delete a notification
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Produces("application/json")]
//[Authorize]
[AllowAnonymous]
public sealed class NotificationController : ControllerBase
{
    private readonly INotificationService                       _notifService;
    private readonly IValidator<SendNotificationRequest>        _sendValidator;
    private readonly IValidator<SendBulkRequest>                _bulkValidator;
    private readonly IValidator<SendEmailRequest>               _emailValidator;
    private readonly ILogger<NotificationController>            _logger;

    public NotificationController(
        INotificationService                    notifService,
        IValidator<SendNotificationRequest>     sendValidator,
        IValidator<SendBulkRequest>             bulkValidator,
        IValidator<SendEmailRequest>            emailValidator,
        ILogger<NotificationController>         logger)
    {
        _notifService   = notifService;
        _sendValidator  = sendValidator;
        _bulkValidator  = bulkValidator;
        _emailValidator = emailValidator;
        _logger         = logger;
    }

    // ── POST /api/v1/notifications/send ──────────────────────────────────────

    /// <summary>Send a single notification (persisted + SignalR push + optional email).</summary>
    /// <response code="201">Notification sent successfully.</response>
    /// <response code="400">Validation error.</response>
    [HttpPost("send")]
    [ProducesResponseType(typeof(NotificationResponse),  StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse),       StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Send([FromBody] SendNotificationRequest request,CancellationToken ct)
    {
        var validation = await _sendValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiErrorResponse("Validation failed.",
                validation.Errors.Select(e => e.ErrorMessage)));

        var response = await _notifService.SendAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    // ── POST /api/v1/notifications/bulk ──────────────────────────────────────

    /// <summary>Admin: broadcast a notification to multiple recipients.</summary>
    /// <response code="200">Bulk notification sent.</response>
    /// <response code="400">Validation error.</response>
    [HttpPost("bulk")]
    //[Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(BulkSendResponse),  StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),   StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendBulk(
        [FromBody] SendBulkRequest request,
        CancellationToken ct)
    {
        var validation = await _bulkValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiErrorResponse("Validation failed.",
                validation.Errors.Select(e => e.ErrorMessage)));

        var response = await _notifService.SendBulkAsync(request, ct);
        return Ok(response);
    }

    // ── POST /api/v1/notifications/email ─────────────────────────────────────

    /// <summary>Send a raw HTML email without persisting a notification record.</summary>
    /// <response code="200">Email sent.</response>
    /// <response code="400">Validation error.</response>
    [HttpPost("email")]
    [ProducesResponseType(typeof(ApiSuccessResponse),  StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),     StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendEmail(
        [FromBody] SendEmailRequest request,
        CancellationToken ct)
    {
        var validation = await _emailValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiErrorResponse("Validation failed.",
                validation.Errors.Select(e => e.ErrorMessage)));

        await _notifService.SendEmailAsync(request, ct);
        return Ok(new ApiSuccessResponse("Email sent successfully."));
    }

    // ── GET /api/v1/notifications/recipient/{recipientId} ────────────────────

    /// <summary>Get paginated notifications for a user.</summary>
    /// <param name="recipientId">The user's ID.</param>
    /// <param name="page">Page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20, max 100).</param>
    [HttpGet("recipient/{recipientId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByRecipient(
        Guid recipientId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, 100);
        var result = await _notifService.GetByRecipientAsync(recipientId, page, pageSize, ct);
        return Ok(result);
    }

    // ── GET /api/v1/notifications/unread/{recipientId} ───────────────────────

    /// <summary>Get the number of unread notifications for a user.</summary>
    [HttpGet("unread/{recipientId:guid}")]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(
        Guid recipientId,
        CancellationToken ct = default)
    {
        var result = await _notifService.GetUnreadCountAsync(recipientId, ct);
        return Ok(result);
    }

    // ── GET /api/v1/notifications/all ────────────────────────────────────────

    /// <summary>Admin: get all notifications (paginated).</summary>
    [HttpGet("all")]
    // //[Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, 200);
        var result = await _notifService.GetAllAsync(page, pageSize, ct);
        return Ok(result);
    }

    // ── PUT /api/v1/notifications/{id}/read ──────────────────────────────────

    /// <summary>Mark a single notification as read.</summary>
    /// <response code="200">Marked as read.</response>
    /// <response code="404">Notification not found.</response>
    [HttpPut("{id:guid}/read")]
    [ProducesResponseType(typeof(ApiSuccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),    StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var success = await _notifService.MarkAsReadAsync(id, ct);
        if (!success)
            return NotFound(new ApiErrorResponse($"Notification {id} not found."));

        return Ok(new ApiSuccessResponse("Notification marked as read."));
    }

    // ── PUT /api/v1/notifications/recipient/{recipientId}/read-all ───────────

    /// <summary>Mark all notifications as read for a given user.</summary>
    [HttpPut("recipient/{recipientId:guid}/read-all")]
    [ProducesResponseType(typeof(ApiSuccessResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead(Guid recipientId, CancellationToken ct)
    {
        await _notifService.MarkAllReadAsync(recipientId, ct);
        return Ok(new ApiSuccessResponse("All notifications marked as read."));
    }

    // ── DELETE /api/v1/notifications/{id} ────────────────────────────────────

    /// <summary>Delete a notification record.</summary>
    /// <response code="200">Deleted.</response>
    /// <response code="404">Not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiSuccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),    StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var success = await _notifService.DeleteAsync(id, ct);
        if (!success)
            return NotFound(new ApiErrorResponse($"Notification {id} not found."));

        return Ok(new ApiSuccessResponse("Notification deleted."));
    }
}
