namespace MediBook.Notification.API.Helpers;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string SecretKey              { get; init; } = string.Empty;
    public string Issuer                 { get; init; } = string.Empty;
    public string Audience               { get; init; } = string.Empty;
    public int    AccessTokenExpiryMinutes { get; init; } = 60;
}
