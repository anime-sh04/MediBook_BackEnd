using FluentValidation;
using MediBook.Payment.API.DTOs;
using MediBook.Payment.API.Entities;

namespace MediBook.Payment.API.Validators;

public sealed class ProcessPaymentRequestValidator : AbstractValidator<ProcessPaymentRequest>
{
    private static readonly IReadOnlySet<string> ValidModes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "UPI", "Card", "NetBanking" };

    public ProcessPaymentRequestValidator()
    {
        RuleFor(x => x.AppointmentId)
            .GreaterThan(0).WithMessage("AppointmentId must be greater than zero.");

        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("PatientId is required.");

        RuleFor(x => x.ProviderId)
            .NotEmpty().WithMessage("ProviderId is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Amount exceeds allowed limit.");

        RuleFor(x => x.Mode)
            .NotEmpty().WithMessage("Payment mode is required.")
            .Must(m => ValidModes.Contains(m))   // ✅ FIX HERE
            .WithMessage($"Mode must be one of: {string.Join(", ", ValidModes)}");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .MaximumLength(10).WithMessage("Currency code must be 10 characters or fewer.");
    }
}

public sealed class ConfirmPaymentRequestValidator : AbstractValidator<ConfirmPaymentRequest>
{
    public ConfirmPaymentRequestValidator()
    {
        RuleFor(x => x.PaymentId)
            .GreaterThan(0).WithMessage("PaymentId must be greater than zero.");

        RuleFor(x => x.RazorpayOrderId)
            .NotEmpty().WithMessage("RazorpayOrderId is required.");

        RuleFor(x => x.RazorpayPaymentId)
            .NotEmpty().WithMessage("RazorpayPaymentId is required.");

        RuleFor(x => x.RazorpaySignature)
            .NotEmpty().WithMessage("RazorpaySignature is required.");

        RuleFor(x => x.TransactionId)
            .NotEmpty().WithMessage("TransactionId is required.");
    }
}
