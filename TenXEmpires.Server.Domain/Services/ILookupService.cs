using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Service for retrieving static lookup data (game rules, definitions, maps).
/// </summary>
public interface ILookupService
{
    /// <summary>
    /// Gets all unit definitions (static game rules data).
    /// </summary>
    /// <returns>A read-only list of unit definitions.</returns>
    Task<IReadOnlyList<UnitDefinitionDto>> GetUnitDefinitionsAsync();

    /// <summary>
    /// Gets the ETag for the current unit definitions data.
    /// </summary>
    /// <returns>An ETag string that represents the current state of unit definitions.</returns>
    Task<string> GetUnitDefinitionsETagAsync();

    /// <summary>
    /// Gets map metadata by its unique code.
    /// </summary>
    /// <param name="code">The unique map code.</param>
    /// <returns>The map metadata, or null if not found.</returns>
    Task<MapDto?> GetMapByCodeAsync(string code);

    /// <summary>
    /// Computes an ETag for a given map based on its metadata.
    /// </summary>
    /// <param name="map">The map DTO to compute the ETag for.</param>
    /// <returns>An ETag string suitable for HTTP caching.</returns>
    string ComputeMapETag(MapDto map);

    /// <summary>
    /// Gets the list of tiles for a given map by code.
    /// </summary>
    /// <param name="code">The unique map code.</param>
    /// <param name="page">Optional 1-based page number for pagination.</param>
    /// <param name="pageSize">Optional page size (default 20, max 100).</param>
    /// <returns>A paged result containing map tiles, or null if map not found.</returns>
    Task<PagedResult<MapTileDto>?> GetMapTilesAsync(string code, int? page = null, int? pageSize = null);

    /// <summary>
    /// Gets the ETag for map tiles by map code.
    /// </summary>
    /// <param name="code">The unique map code.</param>
    /// <param name="page">Optional 1-based page number.</param>
    /// <param name="pageSize">Optional page size.</param>
    /// <returns>An ETag string, or null if map not found.</returns>
    Task<string?> GetMapTilesETagAsync(string code, int? page = null, int? pageSize = null);
}

