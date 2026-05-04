using System.Collections.Concurrent;

namespace MediBook.Auth.API.Services;

/// <summary>
/// Thread-safe in-memory store for OAuth CSRF state tokens.
///
/// Each entry is keyed on the opaque state string and expires after
/// <see cref="StateTtl"/> (default 10 minutes) — long enough for a user to
/// complete the consent flow, short enough to limit replay window.
///
/// In a multi-instance / Kubernetes deployment replace this with a distributed
/// cache (Redis via IDistributedCache) — the interface surface stays the same.
/// </summary>
public sealed class OAuthStateStore
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);

    private sealed record Entry(string Provider, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<string, Entry> _store = new();

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a new cryptographically random state token, stores it,
    /// and returns it to be embedded in the authorization redirect URL.
    /// </summary>
    public string GenerateAndStore(string provider)
    {
        PurgeExpired();

        string state = GenerateSecureToken();
        _store[state] = new Entry(provider, DateTime.UtcNow.Add(StateTtl));
        return state;
    }

    // ── Read / Validate ───────────────────────────────────────────────────────

    /// <summary>
    /// Validates that <paramref name="state"/> was issued by this service for
    /// <paramref name="provider"/> and has not expired.
    /// Consumes the entry (one-time use) on success.
    /// </summary>
    /// <returns>True if valid and consumed; false otherwise.</returns>
    public bool ValidateAndConsume(string state, string provider)
    {
        if (!_store.TryRemove(state, out var entry))
            return false;

        if (entry.ExpiresAt < DateTime.UtcNow)
            return false;

        // Case-insensitive provider match
        return string.Equals(entry.Provider, provider, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void PurgeExpired()
    {
        var now     = DateTime.UtcNow;
        var expired = _store.Where(kv => kv.Value.ExpiresAt < now)
                            .Select(kv => kv.Key)
                            .ToList();
        foreach (var key in expired)
            _store.TryRemove(key, out _);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32]; // 256-bit entropy
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
               .Replace("+", "-")
               .Replace("/", "_")
               .TrimEnd('=');
    }
}
