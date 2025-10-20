using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.Repositories;

/// <summary>
/// Repository for City entity with city-specific queries
/// </summary>
public interface ICityRepository : IRepository<City>
{
    /// <summary>
    /// Get all cities in a game
    /// </summary>
    Task<IEnumerable<City>> GetGameCitiesAsync(long gameId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all cities for a participant
    /// </summary>
    Task<IEnumerable<City>> GetParticipantCitiesAsync(long participantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get city with resources and tiles
    /// </summary>
    Task<City?> GetCityWithDetailsAsync(long cityId, CancellationToken cancellationToken = default);
}

