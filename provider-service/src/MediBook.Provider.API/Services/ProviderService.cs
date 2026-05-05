using MediBook.Provider.API.Data;
using MediBook.Provider.API.DTOs;
using MediBook.Provider.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediBook.Provider.API.Services;

public sealed class ProviderService : IProviderService
{
    private readonly ProviderDbContext        _db;
    private readonly ILogger<ProviderService> _logger;
    private readonly RedisCacheService        _cache;

    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    public ProviderService(
        ProviderDbContext        db,
        ILogger<ProviderService> logger,
        RedisCacheService        cache)
    {
        _db     = db;
        _logger = logger;
        _cache  = cache;
    }

    // ── Registration ──────────────────────────────────────────────────────────

    public async Task<ProviderProfileDto> RegisterProviderAsync(
        Guid userId, RegisterProviderRequest request, CancellationToken ct = default)
    {
        bool exists = await _db.ProviderProfiles.AnyAsync(p => p.UserId == userId, ct);
        if (exists)
            throw new InvalidOperationException("User already has a provider profile.");

        var profile = ProviderProfile.Create(
            userId,
            request.Specialization,
            request.Qualification,
            request.ExperienceYears,
            request.Bio,
            request.ClinicName,
            request.ClinicAddress,
            request.City,
            request.State,
            request.ConsultationFee);

        _db.ProviderProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Provider profile created for UserId: {UserId}", userId);
        return MapToDto(profile);
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<ProviderProfileDto> GetProviderByIdAsync(
        Guid providerId, CancellationToken ct = default)
    {
        string cacheKey = $"providers:{providerId}";

        var cached = await _cache.GetAsync<ProviderProfileDto>(cacheKey);
        if (cached is not null)
            return cached;

        var profile = await _db.ProviderProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProviderId == providerId, ct)
            ?? throw new KeyNotFoundException($"Provider profile '{providerId}' not found.");

        var dto = MapToDto(profile);
        await _cache.SetAsync(cacheKey, dto, CacheExpiry);
        return dto;
    }

    public async Task<ProviderProfileDto> GetMyProfileAsync(
        Guid userId, CancellationToken ct = default)
    {
        var profile = await _db.ProviderProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Provider profile not found for the current user.");
        return MapToDto(profile);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<ProviderProfileDto> UpdateMyProfileAsync(
        Guid userId, RegisterProviderRequest request, CancellationToken ct = default)
    {
        var profile = await _db.ProviderProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Provider profile not found for the current user.");

        profile.UpdateProfile(
            request.Specialization, request.Qualification, request.ExperienceYears,
            request.Bio, request.ClinicName, request.ClinicAddress,
            request.City, request.State, request.ConsultationFee);

        await _db.SaveChangesAsync(ct);

        await _cache.RemoveAsync("providers:list");
        await _cache.RemoveAsync($"providers:{profile.ProviderId}");

        _logger.LogInformation("Provider profile updated for UserId: {UserId}", userId);
        return MapToDto(profile);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task DeleteProviderAsync(Guid providerId, CancellationToken ct = default)
    {
        var profile = await _db.ProviderProfiles
            .FirstOrDefaultAsync(p => p.ProviderId == providerId, ct)
            ?? throw new KeyNotFoundException($"Provider profile '{providerId}' not found.");

        _db.ProviderProfiles.Remove(profile);
        await _db.SaveChangesAsync(ct);

        await _cache.RemoveAsync("providers:list");
        await _cache.RemoveAsync($"providers:{providerId}");

        _logger.LogInformation("Provider profile deleted. ProviderId: {ProviderId}", providerId);
    }

    // ── Search / Browse ───────────────────────────────────────────────────────

    public async Task<PagedResult<ProviderProfileDto>> GetAllProvidersAsync(
        ProviderSearchQuery query, CancellationToken ct = default)
    {
        // Cache only the unfiltered first-page listing (providers:list)
        bool isDefaultQuery =
            query.Specialization is null &&
            query.City           is null &&
            query.IsAvailable    is null &&
            query.Page           == 1    &&
            query.PageSize       == 20;

        if (isDefaultQuery)
        {
            var cached = await _cache.GetAsync<PagedResult<ProviderProfileDto>>("providers:list");
            if (cached is not null)
                return cached;
        }

        var q = _db.ProviderProfiles.AsNoTracking().AsQueryable();

        if (query.IsAvailable.HasValue)
            q = q.Where(p => p.IsAvailable == query.IsAvailable.Value);

        if (!string.IsNullOrWhiteSpace(query.Specialization))
            q = q.Where(p => p.Specialization.ToLower()
                               .Contains(query.Specialization.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(query.City))
            q = q.Where(p => p.City.ToLower()
                               .Contains(query.City.Trim().ToLower()));

        int totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(p => p.AvgRating)
            .ThenBy(p => p.Specialization)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var result = new PagedResult<ProviderProfileDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);

        if (isDefaultQuery)
            await _cache.SetAsync("providers:list", result, CacheExpiry);

        return result;
    }

    public async Task<IReadOnlyList<ProviderProfileDto>> GetBySpecializationAsync(
        string specialization, CancellationToken ct = default)
    {
        var profiles = await _db.ProviderProfiles
            .AsNoTracking()
            .Where(p => p.IsVerified &&
                        p.Specialization.ToLower().Contains(specialization.Trim().ToLower()))
            .OrderByDescending(p => p.AvgRating)
            .ToListAsync(ct);

        return profiles.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<ProviderProfileDto>> SearchProvidersAsync(
        string searchTerm, CancellationToken ct = default)
    {
        string term = searchTerm.Trim().ToLower();

        var profiles = await _db.ProviderProfiles
            .AsNoTracking()
            .Where(p => p.IsVerified && (
                p.Specialization.ToLower().Contains(term) ||
                p.ClinicName.ToLower().Contains(term)     ||
                p.City.ToLower().Contains(term)           ||
                p.ClinicAddress.ToLower().Contains(term)))
            .OrderByDescending(p => p.AvgRating)
            .ToListAsync(ct);

        return profiles.Select(MapToDto).ToList();
    }

    // ── Admin Actions ─────────────────────────────────────────────────────────

    public async Task<ProviderProfileDto> VerifyProviderAsync(
        Guid providerId, bool isVerified, CancellationToken ct = default)
    {
        var profile = await _db.ProviderProfiles
            .FirstOrDefaultAsync(p => p.ProviderId == providerId, ct)
            ?? throw new KeyNotFoundException($"Provider profile '{providerId}' not found.");

        profile.SetVerified(isVerified);
        await _db.SaveChangesAsync(ct);

        await _cache.RemoveAsync("providers:list");
        await _cache.RemoveAsync($"providers:{providerId}");

        _logger.LogInformation(
            "Provider {ProviderId} verification set to {IsVerified}", providerId, isVerified);
        return MapToDto(profile);
    }

    public async Task<ProviderProfileDto> SetAvailabilityAsync(
        Guid providerId, bool isAvailable, CancellationToken ct = default)
    {
        var profile = await _db.ProviderProfiles
            .FirstOrDefaultAsync(p => p.ProviderId == providerId, ct)
            ?? throw new KeyNotFoundException($"Provider profile '{providerId}' not found.");

        profile.SetAvailability(isAvailable);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Provider {ProviderId} availability set to {IsAvailable}", providerId, isAvailable);
        return MapToDto(profile);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    public async Task UpdateRatingAsync(
        Guid providerId, double newAvgRating, CancellationToken ct = default)
    {
        var profile = await _db.ProviderProfiles
            .FirstOrDefaultAsync(p => p.ProviderId == providerId, ct)
            ?? throw new KeyNotFoundException($"Provider profile '{providerId}' not found.");

        profile.UpdateAvgRating(newAvgRating);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "AvgRating updated for Provider {ProviderId}: {NewAvgRating}", providerId, newAvgRating);
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    private static ProviderProfileDto MapToDto(ProviderProfile p) => new(
        p.ProviderId, p.UserId, p.Specialization, p.Qualification,
        p.ExperienceYears, p.Bio, p.ClinicName, p.ClinicAddress,
        p.City, p.State, p.ConsultationFee,
        p.IsVerified, p.IsAvailable, p.AvgRating, p.CreatedAt);
}
