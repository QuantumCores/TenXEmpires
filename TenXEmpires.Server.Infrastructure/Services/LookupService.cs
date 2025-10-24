using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="ILookupService"/> for retrieving static lookup data.
/// </summary>
public class LookupService : ILookupService
{
    private readonly TenXDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LookupService> _logger;
    
    private const string UnitDefinitionsCacheKey = "Lookup.UnitDefinitions";
    private const string UnitDefinitionsETagCacheKey = "Lookup.UnitDefinitions.ETag";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(10);

    public LookupService(
        TenXDbContext context,
        IMemoryCache cache,
        ILogger<LookupService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Gets all unit definitions with caching and efficient querying.
    /// </summary>
    public async Task<IReadOnlyList<UnitDefinitionDto>> GetUnitDefinitionsAsync()
    {
        try
        {
            // Try to get from cache first
            if (_cache.TryGetValue<IReadOnlyList<UnitDefinitionDto>>(UnitDefinitionsCacheKey, out var cached))
            {
                _logger.LogDebug("Unit definitions retrieved from cache");
                return cached!;
            }

            _logger.LogDebug("Fetching unit definitions from database");

            // Query database with AsNoTracking for read-only performance
            // and project directly to DTO to avoid loading navigation properties
            var unitDefinitions = await _context.UnitDefinitions
                .AsNoTracking()
                .OrderBy(u => u.Code)
                .Select(u => new UnitDefinitionDto(
                    u.Id,
                    u.Code,
                    u.IsRanged,
                    u.Attack,
                    u.Defence,
                    u.RangeMin,
                    u.RangeMax,
                    u.MovePoints,
                    u.Health))
                .ToListAsync();

            // Generate ETag based on the data
            var etag = GenerateETag(unitDefinitions);

            // Cache both the results and ETag with absolute expiration
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration
            };

            _cache.Set(UnitDefinitionsCacheKey, (IReadOnlyList<UnitDefinitionDto>)unitDefinitions, cacheOptions);
            _cache.Set(UnitDefinitionsETagCacheKey, etag, cacheOptions);

            _logger.LogInformation("Fetched {Count} unit definitions from database with ETag {ETag}", unitDefinitions.Count, etag);

            return unitDefinitions;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to fetch unit definitions. EventId: {EventId}",
                "Lookup.UnitDefinitions.FetchFailed");
            throw;
        }
    }

    /// <summary>
    /// Gets the ETag for the current unit definitions data.
    /// </summary>
    public async Task<string> GetUnitDefinitionsETagAsync()
    {
        try
        {
            // Check if ETag is cached
            if (_cache.TryGetValue<string>(UnitDefinitionsETagCacheKey, out var cachedETag))
            {
                _logger.LogDebug("Unit definitions ETag retrieved from cache");
                return cachedETag!;
            }

            // If not cached, fetch the data (which will cache both data and ETag)
            var unitDefinitions = await GetUnitDefinitionsAsync();
            
            // ETag should now be cached, retrieve it
            if (_cache.TryGetValue<string>(UnitDefinitionsETagCacheKey, out cachedETag))
            {
                return cachedETag!;
            }

            // Fallback: generate ETag directly
            return GenerateETag(unitDefinitions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unit definitions ETag");
            throw;
        }
    }

    /// <summary>
    /// Generates a stable ETag based on unit definitions data.
    /// Uses count and a hash of codes for efficient change detection.
    /// </summary>
    private static string GenerateETag(IReadOnlyList<UnitDefinitionDto> unitDefinitions)
    {
        // Create a stable representation: count + sorted codes + key stats
        var representation = new StringBuilder();
        representation.Append(unitDefinitions.Count);
        
        foreach (var unit in unitDefinitions)
        {
            representation.Append('|');
            representation.Append(unit.Code);
            representation.Append(':');
            representation.Append(unit.Id);
            representation.Append(':');
            representation.Append(unit.Attack);
            representation.Append(':');
            representation.Append(unit.Defence);
        }

        // Generate SHA256 hash
        var bytes = Encoding.UTF8.GetBytes(representation.ToString());
        var hash = SHA256.HashData(bytes);
        var etag = Convert.ToBase64String(hash);

        return $"\"{etag}\""; // Wrap in quotes per HTTP spec
    }
}

