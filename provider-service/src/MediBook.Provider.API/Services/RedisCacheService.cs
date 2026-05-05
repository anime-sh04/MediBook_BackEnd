using System.Text.Json;
using StackExchange.Redis;

namespace MediBook.Provider.API.Services;

public sealed class RedisCacheService
{
    private readonly IDatabase _db;

    public RedisCacheService(IConnectionMultiplexer mux)
        => _db = mux.GetDatabase();

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(key);
        if (value.IsNullOrEmpty) return default;
        return JsonSerializer.Deserialize<T>(value!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiry)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, expiry);
    }

    public async Task RemoveAsync(string key)
        => await _db.KeyDeleteAsync(key);
}
