using FluentValidation;
using MediBook.Review.API.DTOs;

namespace MediBook.Review.API.Validators;

public sealed class AddReviewRequestValidator : AbstractValidator<AddReviewRequest>
{
    public AddReviewRequestValidator()
    {
        RuleFor(x => x.AppointmentId)
            .GreaterThan(0).WithMessage("AppointmentId must be a positive integer.");

        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("PatientId is required.");

        RuleFor(x => x.ProviderId)
            .NotEmpty().WithMessage("ProviderId is required.");

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5)
            .WithMessage("Rating must be between 1 and 5.");

        RuleFor(x => x.Comment)
            .NotEmpty().WithMessage("Comment is required.")
            .MaximumLength(2000).WithMessage("Comment must not exceed 2000 characters.");
    }
}

public sealed class UpdateReviewRequestValidator : AbstractValidator<UpdateReviewRequest>
{
    public UpdateReviewRequestValidator()
    {
        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5)
            .WithMessage("Rating must be between 1 and 5.");

        RuleFor(x => x.Comment)
            .NotEmpty().WithMessage("Comment is required.")
            .MaximumLength(2000).WithMessage("Comment must not exceed 2000 characters.");
    }
}
