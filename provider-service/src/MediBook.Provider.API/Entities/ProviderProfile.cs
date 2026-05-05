namespace MediBook.Provider.API.Entities;

public class ProviderProfile
{
    public Guid ProviderId { get; private set; }
    public Guid UserId { get; private set; }
    public string Specialization { get; private set; } = string.Empty;
    public string Qualification { get; private set; } = string.Empty;
    public int ExperienceYears { get; private set; }
    public string Bio { get; private set; } = string.Empty;
    public string ClinicName { get; private set; } = string.Empty;
    public string ClinicAddress { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public decimal ConsultationFee { get; private set; }
    public bool IsVerified { get; private set; }
    public bool IsAvailable { get; private set; }
    public double AvgRating { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ProviderProfile() { } // For EF Core

    public static ProviderProfile Create(
        Guid userId, 
        string specialization, 
        string qualification, 
        int experienceYears, 
        string bio, 
        string clinicName, 
        string clinicAddress, 
        string city, 
        string state, 
        decimal consultationFee)
    {
        return new ProviderProfile
        {
            ProviderId = Guid.NewGuid(),
            UserId = userId,
            Specialization = specialization.Trim(),
            Qualification = qualification.Trim(),
            ExperienceYears = experienceYears,
            Bio = bio?.Trim() ?? string.Empty,
            ClinicName = clinicName.Trim(),
            ClinicAddress = clinicAddress.Trim(),
            City = city.Trim(),
            State = state.Trim(),
            ConsultationFee = consultationFee,
            IsVerified = false,
            IsAvailable = true,
            AvgRating = 0.0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(
        string specialization,
        string qualification,
        int experienceYears,
        string bio,
        string clinicName,
        string clinicAddress,
        string city,
        string state,
        decimal consultationFee)
    {
        Specialization = specialization.Trim();
        Qualification = qualification.Trim();
        ExperienceYears = experienceYears;
        Bio = bio?.Trim() ?? string.Empty;
        ClinicName = clinicName.Trim();
        ClinicAddress = clinicAddress.Trim();
        City = city.Trim();
        State = state.Trim();
        ConsultationFee = consultationFee;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetVerified(bool isVerified)
    {
        IsVerified = isVerified;
        UpdatedAt  = DateTime.UtcNow;
    }

    public void SetAvailability(bool isAvailable)
    {
        IsAvailable = isAvailable;
        UpdatedAt   = DateTime.UtcNow;
    }

    public void UpdateAvgRating(double newAvgRating)
    {
        if (newAvgRating < 0 || newAvgRating > 5)
            throw new ArgumentOutOfRangeException(nameof(newAvgRating), "Rating must be between 0 and 5.");
        AvgRating = newAvgRating;
        UpdatedAt = DateTime.UtcNow;
    }
}
