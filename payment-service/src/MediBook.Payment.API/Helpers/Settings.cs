namespace MediBook.Payment.API.Helpers;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer    { get; set; } = string.Empty;
    public string Audience  { get; set; } = string.Empty;
}

public sealed class RazorpaySettings
{
    public const string SectionName = "Razorpay";

    public string KeyId     { get; set; } = string.Empty;
    public string KeySecret { get; set; } = string.Empty;
}
