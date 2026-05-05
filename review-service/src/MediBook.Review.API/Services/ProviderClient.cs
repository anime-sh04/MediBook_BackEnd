using System.Text;
using System.Text.Json;

namespace MediBook.Review.API.Services;

/// <summary>
/// Typed HTTP client that calls the provider-service to sync the AvgRating field.
/// Registered via IHttpClientFactory in ServiceCollectionExtensions.
/// </summary>
public sealed class ProviderClient : IProviderClient
{
    private readonly HttpClient                    _http;
    private readonly ILogger<ProviderClient>       _logger;

    public ProviderClient(HttpClient http, ILogger<ProviderClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task UpdateProviderRatingAsync(
        Guid providerId, double avgRating, CancellationToken ct = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { avgRating });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await _http.PutAsync(
                $"api/v1/providers/{providerId}/rating", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "provider-service returned {StatusCode} when updating rating for provider {ProviderId}.",
                    (int)response.StatusCode, providerId);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: log and continue; provider-service outage should not break reviews
            _logger.LogError(ex,
                "Failed to notify provider-service of rating update for provider {ProviderId}.",
                providerId);
        }
    }
}
