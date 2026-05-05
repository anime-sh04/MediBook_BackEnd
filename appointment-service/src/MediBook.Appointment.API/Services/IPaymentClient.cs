namespace MediBook.Appointment.API.Services;

/// <summary>
/// Stub interface for the Payment-Service.
/// Replace with a real typed HTTP client once the payment-service is built.
/// </summary>
public interface IPaymentClient
{
    /// <summary>
    /// Triggers a refund for a cancelled appointment.
    /// No-ops in the current stub implementation.
    /// </summary>
    Task RefundAsync(int appointmentId, CancellationToken ct = default);
}

/// <summary>
/// Stub implementation — logs the refund intent without making real HTTP calls.
/// Replace the body of <see cref="RefundAsync"/> once payment-service is live.
/// </summary>
public sealed class PaymentClientStub : IPaymentClient
{
    private readonly ILogger<PaymentClientStub> _logger;

    public PaymentClientStub(ILogger<PaymentClientStub> logger) => _logger = logger;

    public Task RefundAsync(int appointmentId, CancellationToken ct = default)
    {
        // TODO: Replace with real HTTP call to payment-service
        // POST /api/v1/payments/refund  { AppointmentId = appointmentId }
        _logger.LogInformation(
            "[STUB] Refund triggered for AppointmentId: {AppointmentId}. " +
            "Wire up the real payment-service client here.", appointmentId);
        return Task.CompletedTask;
    }
}
