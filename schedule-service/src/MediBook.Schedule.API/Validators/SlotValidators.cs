using FluentValidation;
using MediBook.Schedule.API.DTOs;

namespace MediBook.Schedule.API.Validators;

public sealed class AddSlotRequestValidator : AbstractValidator<AddSlotRequest>
{
    public AddSlotRequestValidator()
    {
        RuleFor(x => x.ProviderId)
            .NotEmpty()
            .NotEqual(Guid.Empty);

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Date is required.")
            .Must(BeValidDate).WithMessage("Date must be a valid date in yyyy-MM-dd format.");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("StartTime is required.")
            .Must(BeValidTime).WithMessage("StartTime must be in HH:mm format.");

        RuleFor(x => x.EndTime)
            .NotEmpty().WithMessage("EndTime is required.")
            .Must(BeValidTime).WithMessage("EndTime must be in HH:mm format.");

        RuleFor(x => x)
            .Must(x => TimeOnly.TryParse(x.EndTime, out var end) &&
                        TimeOnly.TryParse(x.StartTime, out var start) &&
                        end > start)
            .WithMessage("EndTime must be after StartTime.")
            .When(x => BeValidTime(x.StartTime) && BeValidTime(x.EndTime));

        RuleFor(x => x.Recurrence)
            .Must(r => r is null or "none" or "daily" or "weekly")
            .WithMessage("Recurrence must be 'none', 'daily', or 'weekly'.");
    }

    private static bool BeValidDate(string? s) =>
        !string.IsNullOrWhiteSpace(s) && DateOnly.TryParse(s, out _);

    private static bool BeValidTime(string? s) =>
        !string.IsNullOrWhiteSpace(s) && TimeOnly.TryParse(s, out _);
}

public sealed class AddBulkSlotsRequestValidator : AbstractValidator<AddBulkSlotsRequest>
{
    public AddBulkSlotsRequestValidator()
    {
        RuleFor(x => x.Slots)
            .NotNull().WithMessage("Slots list is required.")
            .Must(s => s is { Count: > 0 }).WithMessage("At least one slot is required.")
            .Must(s => s.Count <= 500).WithMessage("Cannot bulk-create more than 500 slots at once.");

        RuleForEach(x => x.Slots)
            .SetValidator(new AddSlotRequestValidator());
    }
}

public sealed class UpdateSlotRequestValidator : AbstractValidator<UpdateSlotRequest>
{
    public UpdateSlotRequestValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Date is required.")
            .Must(s => DateOnly.TryParse(s, out _)).WithMessage("Date must be in yyyy-MM-dd format.");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("StartTime is required.")
            .Must(s => TimeOnly.TryParse(s, out _)).WithMessage("StartTime must be in HH:mm format.");

        RuleFor(x => x.EndTime)
            .NotEmpty().WithMessage("EndTime is required.")
            .Must(s => TimeOnly.TryParse(s, out _)).WithMessage("EndTime must be in HH:mm format.");

        RuleFor(x => x)
            .Must(x => TimeOnly.TryParse(x.EndTime, out var end) &&
                        TimeOnly.TryParse(x.StartTime, out var start) &&
                        end > start)
            .WithMessage("EndTime must be after StartTime.");

        RuleFor(x => x.Recurrence)
            .Must(r => r is null or "none" or "daily" or "weekly")
            .WithMessage("Recurrence must be 'none', 'daily', or 'weekly'.")
            .When(x => x.Recurrence is not null);
    }
}

public sealed class GenerateRecurringRequestValidator : AbstractValidator<GenerateRecurringRequest>
{
    public GenerateRecurringRequestValidator()
    {
        RuleFor(x => x.ProviderId)
            .NotEmpty()
            .NotEqual(Guid.Empty);

        RuleFor(x => x.Recurrence)
            .NotEmpty().WithMessage("Recurrence is required.")
            .Must(r => r is "daily" or "weekly").WithMessage("Recurrence must be 'daily' or 'weekly'.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("StartDate is required.")
            .Must(s => DateOnly.TryParse(s, out _)).WithMessage("StartDate must be in yyyy-MM-dd format.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("EndDate is required.")
            .Must(s => DateOnly.TryParse(s, out _)).WithMessage("EndDate must be in yyyy-MM-dd format.");

        RuleFor(x => x)
            .Must(x => DateOnly.TryParse(x.EndDate, out var end) &&
                        DateOnly.TryParse(x.StartDate, out var start) &&
                        end >= start)
            .WithMessage("EndDate must be on or after StartDate.")
            .When(x => DateOnly.TryParse(x.StartDate, out _) && DateOnly.TryParse(x.EndDate, out _));

        RuleFor(x => x.SlotStartTime)
            .NotEmpty().WithMessage("SlotStartTime is required.")
            .Must(s => TimeOnly.TryParse(s, out _)).WithMessage("SlotStartTime must be in HH:mm format.");

        RuleFor(x => x.SlotEndTime)
            .NotEmpty().WithMessage("SlotEndTime is required.")
            .Must(s => TimeOnly.TryParse(s, out _)).WithMessage("SlotEndTime must be in HH:mm format.");

        RuleFor(x => x)
            .Must(x => TimeOnly.TryParse(x.SlotEndTime, out var end) &&
                        TimeOnly.TryParse(x.SlotStartTime, out var start) &&
                        end > start)
            .WithMessage("SlotEndTime must be after SlotStartTime.");
    }
}
