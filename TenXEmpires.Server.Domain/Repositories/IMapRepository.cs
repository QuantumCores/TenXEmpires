using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.Repositories;

/// <summary>
/// Repository for Map entity with map-specific queries
/// </summary>
public interface IMapRepository : IRepository<Map>
{
    /// <summary>
    /// Get map by code
    /// </summary>
    Task<Map?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get map with all tiles included
    /// </summary>
    Task<Map?> GetMapWithTilesAsync(long mapId, CancellationToken cancellationToken = default);
}

