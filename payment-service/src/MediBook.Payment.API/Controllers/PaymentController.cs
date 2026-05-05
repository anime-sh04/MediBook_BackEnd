using MediBook.Payment.API.DTOs;
using MediBook.Payment.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediBook.Payment.API.Controllers;

/// <summary>
/// ════════════════════════════════════════════════════════════════════════════
///  PaymentController — Saga + Razorpay API
///  Base route: /api/v1/payments
/// ════════════════════════════════════════════════════════════════════════════
///
/// Saga flow this controller drives:
///
///   1. PUT  /slots/{id}/book          (Schedule Service) → publishes PaymentRequested
///   2.  → Consumer stores CorrelationId + creates Razorpay order (status = PENDING)
///   3. GET  /payments/slot/{slotId}   → frontend fetches orderId + paymentId
///   4.  → Frontend opens Razorpay checkout widget
///   5a. POST /payments/confirm        → signature verified → PaymentSucceeded published
///   5b. POST /payments/fail           → payment marked failed → PaymentFailed published
///   6.  → Schedule Service consumer confirms or rolls back the slot
///
/// Alternatively, the frontend can call POST /payments/process directly
/// (bypassing the consumer) to create the order and get back orderId in one
/// HTTP round-trip.
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentController : ControllerBase
{
    private readonly IPaymentService            _payService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService            payService,
        ILogger<PaymentController> logger)
    {
        _payService = payService;
        _logger     = logger;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  POST /api/v1/payments/process
    //
    //  Creates a Razorpay order (online modes) or a Pending Cash record.
    //  Returns orderId so the frontend can open the Razorpay checkout widget.
    //  Does NOT process or confirm the payment.
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initiate a payment for a slot booking.
    /// For online modes (Card/UPI/Wallet) returns a Razorpay orderId the frontend
    /// uses to render the checkout widget.
    /// For Cash, records a Pending entry.
    /// Requires <c>CorrelationId</c> and <c>SlotId</c> from the Saga
    /// (returned by PUT /slots/{id}/book).
    /// </summary>
    /// <response code="201">Payment record created. Includes razorpayOrder for online modes.</response>
    /// <response code="400">Invalid request body.</response>
    /// <response code="409">A payment already exists for this appointment.</response>
    [HttpPost("process")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Process(
        [FromBody] ProcessPaymentRequest request,
        CancellationToken ct)
    {
        try
        {
            var (payment, razorpayOrder) = await _payService.ProcessPaymentAsync(request, ct);

            // Shape response based on mode
            object result = razorpayOrder is null
                ? (object)payment
                : new { payment, razorpayOrder };

            return CreatedAtAction(
                nameof(GetByAppointment),
                new { appointmentId = payment.AppointmentId },
                result);
        }
        catch (InvalidOperationException ex) { return Conflict(new ApiErrorResponse(ex.Message)); }
        catch (ArgumentException         ex) { return BadRequest(new ApiErrorResponse(ex.Message)); }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  POST /api/v1/payments/confirm
    //
    //  ★ SAGA SUCCESS PATH
    //  Called by the frontend after the user completes the Razorpay checkout.
    //  1. Verifies HMAC-SHA256 signature (proves gateway captured the money).
    //  2. Marks payment Status = Paid.
    //  3. Publishes PaymentSucceeded → Schedule Service confirms the slot.
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verify Razorpay signature and confirm payment.
    /// Called by the frontend after Razorpay checkout handler fires
    /// <c>payment.success</c>.
    /// Publishes <c>PaymentSucceeded</c> which causes the Schedule Service to
    /// mark the slot as CONFIRMED (IsBooked = true).
    /// </summary>
    /// <response code="200">Payment confirmed. Slot booking finalised.</response>
    /// <response code="400">Signature verification failed or bad request body.</response>
    /// <response code="404">Payment not found.</response>
    /// <response code="409">Payment is not in Pending status.</response>
    [HttpPost("confirm")]
    [ProducesResponseType(typeof(ConfirmPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(
        [FromBody] ConfirmPaymentRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Confirm payment called. PaymentId={PaymentId}, " +
            "RazorpayOrderId={OrderId}, RazorpayPaymentId={RpId}",
            request.PaymentId, request.RazorpayOrderId, request.RazorpayPaymentId);

        try
        {
            var response = await _payService.ConfirmPaymentAsync(request, ct);
            return Ok(response);
        }
        catch (KeyNotFoundException   ex) { return NotFound(new ApiErrorResponse(ex.Message)); }
        catch (ArgumentException      ex) { return BadRequest(new ApiErrorResponse(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(new ApiErrorResponse(ex.Message)); }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  POST /api/v1/payments/fail
    //
    //  ★ SAGA FAILURE / COMPENSATION PATH
    //  Called by the frontend when the user dismisses the checkout widget or
    //  when the gateway fires payment.error / payment.failed.
    //  1. Marks payment Status = Failed.
    //  2. Publishes PaymentFailed → Schedule Service rolls the slot back to AVAILABLE.
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Mark a payment as failed and trigger Saga compensation.
    /// Called by the frontend after Razorpay checkout fires
    /// <c>payment.error</c> or when the user dismisses the widget.
    /// Publishes <c>PaymentFailed</c> which causes the Schedule Service to
    /// release the slot back to AVAILABLE.
    /// </summary>
    /// <response code="200">Payment marked failed. Slot released.</response>
    /// <response code="400">Bad request body.</response>
    /// <response code="404">Payment not found.</response>
    /// <response code="409">Payment is not in Pending status.</response>
    [HttpPost("fail")]
    [ProducesResponseType(typeof(FailPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse),    StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse),    StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse),    StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Fail(
        [FromBody] FailPaymentRequest request,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "Fail payment called. PaymentId={PaymentId}, Reason={Reason}",
            request.PaymentId, request.Reason ?? "(none)");

        try
        {
            var response = await _payService.FailPaymentAsync(request, ct);
            return Ok(response);
        }
        catch (KeyNotFoundException      ex) { return NotFound(new ApiErrorResponse(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(new ApiErrorResponse(ex.Message)); }
        catch (ArgumentException         ex) { return BadRequest(new ApiErrorResponse(ex.Message)); }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  GET /api/v1/payments/slot/{slotId}
    //
    //  Frontend calls this after PUT /slots/{id}/book to retrieve the
    //  Razorpay orderId created by the consumer, then opens the checkout widget.
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the payment for a given slot.
    /// The frontend calls this after initiating the booking Saga to get the
    /// Razorpay orderId and paymentId needed to open the checkout widget.
    /// </summary>
    /// <response code="200">Payment found.</response>
    /// <response code="404">No payment exists for this slot yet.</response>
    [HttpGet("slot/{slotId:int}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlot(int slotId, CancellationToken ct)
    {
        var dto = await _payService.GetPaymentBySlotAsync(slotId, ct);
        if (dto is null)
            return NotFound(new ApiErrorResponse(
                $"No payment found for slotId {slotId}. " +
                "The consumer may still be processing the PaymentRequested event."));
        return Ok(dto);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Existing query endpoints (unchanged)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Returns the payment linked to a specific appointment.</summary>
    [HttpGet("appointment/{appointmentId:int}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByAppointment(int appointmentId, CancellationToken ct)
    {
        var dto = await _payService.GetPaymentByAppointmentAsync(appointmentId, ct);
        if (dto is null)
            return NotFound(new ApiErrorResponse(
                $"No payment found for appointmentId {appointmentId}."));
        return Ok(dto);
    }

    /// <summary>Returns all payments made by a patient.</summary>
    [HttpGet("patient/{patientId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPatient(Guid patientId, CancellationToken ct)
        => Ok(await _payService.GetPaymentsByPatientAsync(patientId, ct));

    /// <summary>Returns all payment transactions across the platform. Admin only.</summary>
    [HttpGet("history")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(CancellationToken ct)
        => Ok(await _payService.GetPaymentHistoryAsync(ct));

    /// <summary>Returns the current status string for a payment.</summary>
    [HttpGet("{paymentId:int}/status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(int paymentId, CancellationToken ct)
    {
        try
        {
            var status = await _payService.GetPaymentStatusAsync(paymentId, ct);
            return Ok(new { paymentId, status });
        }
        catch (KeyNotFoundException ex) { return NotFound(new ApiErrorResponse(ex.Message)); }
    }

    /// <summary>Admin override to update a payment's status.</summary>
    [HttpPut("{paymentId:int}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        int paymentId,
        [FromBody] UpdateStatusRequest request,
        CancellationToken ct)
    {
        try
        {
            var dto = await _payService.UpdatePaymentStatusAsync(paymentId, request.Status, ct);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex) { return NotFound(new ApiErrorResponse(ex.Message)); }
        catch (ArgumentException    ex) { return BadRequest(new ApiErrorResponse(ex.Message)); }
    }

    /// <summary>Returns invoice details for a completed (Paid) payment.</summary>
    [HttpGet("{paymentId:int}/invoice")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateInvoice(int paymentId, CancellationToken ct)
    {
        try
        {
            var invoice = await _payService.GenerateInvoiceAsync(paymentId, ct);
            return Ok(invoice);
        }
        catch (KeyNotFoundException      ex) { return NotFound(new ApiErrorResponse(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(new ApiErrorResponse(ex.Message)); }
    }

    /// <summary>Returns total paid revenue for a provider.</summary>
    [HttpGet("revenue/{providerId:guid}")]
    [Authorize(Roles = "Provider,Admin")]
    [ProducesResponseType(typeof(TotalRevenueDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTotalRevenue(Guid providerId, CancellationToken ct)
        => Ok(await _payService.GetTotalRevenueAsync(providerId, ct));
}

/// <summary>Request body for admin status-override endpoint.</summary>
public sealed record UpdateStatusRequest(string Status);
