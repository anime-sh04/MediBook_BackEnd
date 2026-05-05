using System.Net;
using System.Text.Json;
using MediBook.Appointment.API.DTOs;

namespace MediBook.Appointment.API.Services;

public sealed class ScheduleClient : IScheduleClient
{
    private readonly HttpClient             _http;
    private readonly ILogger<ScheduleClient> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ScheduleClient(HttpClient http, ILogger<ScheduleClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    // ── GET SLOT ─────────────────────────────────────────────────────────────

    public async Task<SlotDto?> GetSlotAsync(int slotId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/v1/slots/{slotId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<SlotDto>(content, _jsonOpts);
    }

    // ── UNBOOK SLOT ──────────────────────────────────────────────────────────
    // Called only on appointment cancellation.  The token is forwarded from
    // the current HTTP request context via DI — no IHttpContextAccessor needed
    // because we pass the token explicitly from the service layer.

    public async Task UnbookSlotAsync(int slotId, CancellationToken ct = default)
    {
        _logger.LogInformation("[Appointment] Calling schedule-service: unbook slot {SlotId}", slotId);

        var response = await _http.PutAsync($"api/v1/slots/{slotId}/unbook", null, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("[Appointment] Slot {SlotId} not found during unbook. Continuing.", slotId);
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "[Appointment] schedule-service returned {Status} while unbooking slot {SlotId}: {Body}",
                response.StatusCode, slotId, body);
        }
    }
}
