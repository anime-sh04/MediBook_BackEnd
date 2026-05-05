using FluentValidation;
using MediBook.Appointment.API.DTOs;

namespace MediBook.Appointment.API.Validators;

public sealed class RescheduleRequestValidator : AbstractValidator<RescheduleRequest>
{
    public RescheduleRequestValidator()
    {
        RuleFor(x => x.NewSlotId)
            .GreaterThan(0).WithMessage("NewSlotId must be a positive integer.");

        RuleFor(x => x.NewAppointmentDate)
            .NotEmpty().WithMessage("NewAppointmentDate is required.")
            .Must(s => DateOnly.TryParse(s, out _))
            .WithMessage("NewAppointmentDate must be in yyyy-MM-dd format.");

        RuleFor(x => x.NewStartTime)
            .NotEmpty().WithMessage("NewStartTime is required.")
            .Must(s => TimeOnly.TryParse(s, out _))
            .WithMessage("NewStartTime must be in HH:mm format.");

        RuleFor(x => x.NewEndTime)
            .NotEmpty().WithMessage("NewEndTime is required.")
            .Must(s => TimeOnly.TryParse(s, out _))
            .WithMessage("NewEndTime must be in HH:mm format.");

        RuleFor(x => x)
            .Must(x => TimeOnly.TryParse(x.NewEndTime,   out var e) &&
                       TimeOnly.TryParse(x.NewStartTime, out var s) && e > s)
            .WithMessage("NewEndTime must be after NewStartTime.")
            .When(x => TimeOnly.TryParse(x.NewStartTime, out _) &&
                       TimeOnly.TryParse(x.NewEndTime,   out _));
    }
}

public sealed class UpdateStatusRequestValidator : AbstractValidator<UpdateStatusRequest>
{
    private static readonly IReadOnlySet<string> ValidStatuses =
        new HashSet<string> { "Scheduled", "Completed", "Cancelled", "No-Show" };

    public UpdateStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => ValidStatuses.Contains(s))
            .WithMessage(
                $"Status must be one of: {string.Join(", ", ValidStatuses)}.");
    }
}
