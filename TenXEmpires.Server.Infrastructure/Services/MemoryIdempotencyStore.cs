using Microsoft.Extensions.Caching.Memory;
using TenXEmpires.Server.Domain.Services;

namespace TenXEmpires.Server.Infrastructure.Services;

/// <summary>
/// Memory-based implementation of idempotency store using IMemoryCache.
/// Suitable for single-server deployments; use distributed cache for multi-server.
/// </summary>
public class MemoryIdempotencyStore : IIdempotencyStore
{
    private readonly IMemoryCache _cache;

    public MemoryIdempotencyStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<bool> TryStoreAsync<T>(
        string key,
        T response,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"idempotency:{key}";
        
        // Try to get existing entry
        if (_cache.TryGetValue(cacheKey, out _))
        {
            // Key already exists, cannot store
            return Task.FromResult(false);
        }

        // Store the response with expiration
        _cache.Set(cacheKey, response, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        });

        return Task.FromResult(true);
    }

    public Task<T?> TryGetAsync<T>(
        string key,
        CancellationToken cancellationToken = default) where T : class
    {
        var cacheKey = $"idempotency:{key}";
        
        if (_cache.TryGetValue<T>(cacheKey, out var response))
        {
            return Task.FromResult<T?>(response);
        }

        return Task.FromResult<T?>(null);
    }
}

