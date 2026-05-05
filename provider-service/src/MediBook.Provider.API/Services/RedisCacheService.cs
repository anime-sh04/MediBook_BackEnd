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
        try
        {
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty) return default;
    
            return JsonSerializer.Deserialize<T>(value!);
        }
        catch (Exception ex)
        {
            Console.WriteLine("⚠️ Redis failed: " + ex.Message);
            return default; // fallback to DB
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _db.StringSetAsync(key, json, expiry);
        }
        catch (Exception ex)
        {
            Console.WriteLine("⚠️ Redis SET failed: " + ex.Message);
        }
    }

    public async Task RemoveAsync(string key)
        => await _db.KeyDeleteAsync(key);
}
