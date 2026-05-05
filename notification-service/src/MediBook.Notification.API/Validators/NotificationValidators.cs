using FluentValidation;
using MediBook.Notification.API.DTOs;
using MediBook.Notification.API.Entities;

namespace MediBook.Notification.API.Validators;

public sealed class SendNotificationRequestValidator : AbstractValidator<SendNotificationRequest>
{
    private static readonly string[] ValidTypes = {
        NotificationTypes.Booking,
        NotificationTypes.Reminder,
        NotificationTypes.Cancellation,
        NotificationTypes.Payment,
        NotificationTypes.FollowUp
    };

    private static readonly string[] ValidChannels = {
        NotificationChannels.App,
        NotificationChannels.Email,
        NotificationChannels.Sms
    };

    public SendNotificationRequestValidator()
    {
        RuleFor(x => x.RecipientId)
            .NotEmpty().WithMessage("RecipientId is required.");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required.")
            .Must(t => ValidTypes.Contains(t?.ToUpperInvariant()))
            .WithMessage($"Type must be one of: {string.Join(", ", ValidTypes)}.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.")
            .MaximumLength(2000).WithMessage("Message must not exceed 2000 characters.");

        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("Channel is required.")
            .Must(c => ValidChannels.Contains(c?.ToUpperInvariant()))
            .WithMessage($"Channel must be one of: {string.Join(", ", ValidChannels)}.");

        // RecipientEmail is required when channel is EMAIL
        RuleFor(x => x.RecipientEmail)
            .NotEmpty().WithMessage("RecipientEmail is required for EMAIL channel.")
            .EmailAddress().WithMessage("RecipientEmail must be a valid email address.")
            .When(x => x.Channel?.ToUpperInvariant() == NotificationChannels.Email);
    }
}

public sealed class SendBulkRequestValidator : AbstractValidator<SendBulkRequest>
{
    public SendBulkRequestValidator()
    {
        RuleFor(x => x.RecipientIds)
            .NotEmpty().WithMessage("At least one RecipientId is required.")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("All RecipientIds must be valid non-empty GUIDs.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200);

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.")
            .MaximumLength(2000);

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required.");

        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("Channel is required.");
    }
}

public sealed class SendEmailRequestValidator : AbstractValidator<SendEmailRequest>
{
    public SendEmailRequestValidator()
    {
        RuleFor(x => x.ToEmail)
            .NotEmpty().WithMessage("ToEmail is required.")
            .EmailAddress().WithMessage("ToEmail must be a valid email address.");

        RuleFor(x => x.ToName)
            .NotEmpty().WithMessage("ToName is required.");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MaximumLength(200);

        RuleFor(x => x.HtmlBody)
            .NotEmpty().WithMessage("HtmlBody is required.");
    }
}
