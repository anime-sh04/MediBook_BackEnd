namespace MediBook.Review.API.Helpers;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer    { get; set; } = string.Empty;
    public string Audience  { get; set; } = string.Empty;
}
