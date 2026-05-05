using FluentValidation;
using MediBook.Provider.API.DTOs;

namespace MediBook.Provider.API.Validators;

public sealed class RegisterProviderRequestValidator : AbstractValidator<RegisterProviderRequest>
{
    public RegisterProviderRequestValidator()
    {
        RuleFor(x => x.Specialization)
            .NotEmpty().WithMessage("Specialization is required.")
            .MaximumLength(100).WithMessage("Maximum length is 100.");

        RuleFor(x => x.Qualification)
            .NotEmpty().WithMessage("Qualification is required.")
            .MaximumLength(200).WithMessage("Maximum length is 200.");

        RuleFor(x => x.ExperienceYears)
            .InclusiveBetween(0, 60).WithMessage("Experience must be between 0 and 60 years.");

        RuleFor(x => x.Bio)
            .MaximumLength(1000).WithMessage("Maximum length is 1000.");

        RuleFor(x => x.ClinicName)
            .NotEmpty().WithMessage("Clinic Name is required.")
            .MaximumLength(200).WithMessage("Maximum length is 200.");

        RuleFor(x => x.ClinicAddress)
            .NotEmpty().WithMessage("Clinic Address is required.")
            .MaximumLength(500).WithMessage("Maximum length is 500.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(100).WithMessage("Maximum length is 100.");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("State is required.")
            .MaximumLength(100).WithMessage("Maximum length is 100.");

        RuleFor(x => x.ConsultationFee)
            .GreaterThan(0).WithMessage("Fee must be greater than 0.");
    }
}
