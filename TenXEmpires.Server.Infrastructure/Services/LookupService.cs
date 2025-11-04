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
    private const string MapByCodeCacheKeyPrefix = "Lookup.Map.";
    private const string MapTilesCacheKeyPrefix = "Lookup.MapTiles.";
    private const string MapTilesETagCacheKeyPrefix = "Lookup.MapTiles.ETag.";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(10);
    
    private const int DefaultPageSize = 500;
    private const int MaxPageSize = 500;

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

    /// <summary>
    /// Gets map metadata by its unique code with caching.
    /// </summary>
    public async Task<MapDto?> GetMapByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code must be provided", nameof(code));
        }

        try
        {
            var cacheKey = MapByCodeCacheKeyPrefix + code;

            if (_cache.TryGetValue<MapDto?>(cacheKey, out var cached))
            {
                _logger.LogDebug("Map {Code} retrieved from cache", code);
                return cached!; // may be null if not found cached
            }

            _logger.LogDebug("Fetching map {Code} from database", code);

            // Query database with AsNoTracking and project directly to DTO
            var map = await _context.Maps
                .AsNoTracking()
                .Where(m => m.Code == code)
                .Select(m => new MapDto(
                    m.Id,
                    m.Code,
                    m.SchemaVersion,
                    m.Width,
                    m.Height))
                .FirstOrDefaultAsync();

            // Cache the result (including null to avoid repeated misses) with short TTL
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration
            };
            _cache.Set(cacheKey, map, cacheOptions);

            if (map is null)
            {
                _logger.LogInformation("Map {Code} not found", code);
            }
            else
            {
                _logger.LogInformation("Map {Code} retrieved with Id {Id}", code, map.Id);
            }

            return map;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch map by code {Code}. EventId: {EventId}", code, "Lookup.Maps.GetByCodeFailed");
            throw;
        }
    }

    /// <summary>
    /// Gets map tiles by map code with optional pagination and caching.
    /// </summary>
    public async Task<PagedResult<MapTileDto>?> GetMapTilesAsync(string code, int? page = null, int? pageSize = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code must be provided", nameof(code));
        }

        // Validate and normalize pagination parameters
        var effectivePage = page ?? 1;
        var effectivePageSize = pageSize ?? DefaultPageSize;

        if (effectivePage < 1)
        {
            throw new ArgumentException("Page must be >= 1", nameof(page));
        }

        if (effectivePageSize < 1 || effectivePageSize > MaxPageSize)
        {
            throw new ArgumentException($"PageSize must be between 1 and {MaxPageSize}", nameof(pageSize));
        }

        try
        {
            // Check if map exists first (this is cached)
            var map = await GetMapByCodeAsync(code);
            if (map is null)
            {
                return null;
            }

            // Build cache key including pagination parameters
            var cacheKey = $"{MapTilesCacheKeyPrefix}{code}:p{effectivePage}:ps{effectivePageSize}";

            if (_cache.TryGetValue<PagedResult<MapTileDto>>(cacheKey, out var cached))
            {
                _logger.LogDebug("Map tiles for {Code} (page {Page}, size {PageSize}) retrieved from cache", code, effectivePage, effectivePageSize);
                return cached!;
            }

            _logger.LogDebug("Fetching map tiles for {Code} (page {Page}, size {PageSize}) from database", code, effectivePage, effectivePageSize);

            // Query with AsNoTracking and project to DTO
            // Order by row, then col for stable pagination
            var skip = (effectivePage - 1) * effectivePageSize;

            var tiles = await _context.MapTiles
                .AsNoTracking()
                .Where(t => t.MapId == map.Id)
                .OrderBy(t => t.Row)
                .ThenBy(t => t.Col)
                .Skip(skip)
                .Take(effectivePageSize)
                .Select(t => new MapTileDto(
                    t.Id,
                    t.Row,
                    t.Col,
                    t.Terrain,
                    t.ResourceType,
                    t.ResourceAmount))
                .ToListAsync();

            // Get total count for pagination metadata (only for first page to optimize)
            // For large maps, we can cache the count separately
            int? total = null;
            if (effectivePage == 1)
            {
                total = await _context.MapTiles
                    .AsNoTracking()
                    .Where(t => t.MapId == map.Id)
                    .CountAsync();
            }

            var result = new PagedResult<MapTileDto>
            {
                Items = tiles,
                Page = effectivePage,
                PageSize = effectivePageSize,
                Total = total
            };

            // Cache the result
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration
            };
            _cache.Set(cacheKey, result, cacheOptions);

            _logger.LogInformation(
                "Fetched {Count} tiles for map {Code} (page {Page}, size {PageSize})",
                tiles.Count,
                code,
                effectivePage,
                effectivePageSize);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to fetch map tiles for code {Code}. EventId: {EventId}",
                code,
                "Lookup.MapTiles.FetchFailed");
            throw;
        }
    }

    /// <summary>
    /// Gets the ETag for map tiles by map code and pagination parameters.
    /// </summary>
    public async Task<string?> GetMapTilesETagAsync(string code, int? page = null, int? pageSize = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code must be provided", nameof(code));
        }

        // Validate and normalize pagination parameters
        var effectivePage = page ?? 1;
        var effectivePageSize = pageSize ?? DefaultPageSize;

        if (effectivePage < 1)
        {
            throw new ArgumentException("Page must be >= 1", nameof(page));
        }

        if (effectivePageSize < 1 || effectivePageSize > MaxPageSize)
        {
            throw new ArgumentException($"PageSize must be between 1 and {MaxPageSize}", nameof(pageSize));
        }

        try
        {
            var etagCacheKey = $"{MapTilesETagCacheKeyPrefix}{code}:p{effectivePage}:ps{effectivePageSize}";

            // Check if ETag is cached
            if (_cache.TryGetValue<string>(etagCacheKey, out var cachedETag))
            {
                _logger.LogDebug("Map tiles ETag for {Code} retrieved from cache", code);
                return cachedETag!;
            }

            // If not cached, fetch the tiles (which will cache both tiles and generate ETag)
            var tiles = await GetMapTilesAsync(code, effectivePage, effectivePageSize);
            if (tiles is null)
            {
                return null;
            }

            // Generate ETag based on map code, pagination, and tile count
            var etag = GenerateMapTilesETag(code, effectivePage, effectivePageSize, tiles.Items.Count, tiles.Total);

            // Cache the ETag
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration
            };
            _cache.Set(etagCacheKey, etag, cacheOptions);

            return etag;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get map tiles ETag for code {Code}", code);
            throw;
        }
    }

    /// <summary>
    /// Computes an ETag for a given map based on its metadata.
    /// </summary>
    public string ComputeMapETag(MapDto map)
    {
        var representation = $"{map.Code}:{map.SchemaVersion}:{map.Width}:{map.Height}";
        var bytes = Encoding.UTF8.GetBytes(representation);
        var hash = SHA256.HashData(bytes);
        var etag = Convert.ToBase64String(hash);
        return $"\"{etag}\""; // Wrap in quotes per HTTP spec
    }

    /// <summary>
    /// Generates a stable ETag for map tiles based on map code, pagination, and metadata.
    /// </summary>
    private static string GenerateMapTilesETag(string code, int page, int pageSize, int itemCount, int? total)
    {
        // Create a stable representation based on map code, pagination params, and counts
        var representation = $"{code}:p{page}:ps{pageSize}:cnt{itemCount}:tot{total}";
        var bytes = Encoding.UTF8.GetBytes(representation);
        var hash = SHA256.HashData(bytes);
        var etag = Convert.ToBase64String(hash);
        return $"\"{etag}\""; // Wrap in quotes per HTTP spec
    }
}

