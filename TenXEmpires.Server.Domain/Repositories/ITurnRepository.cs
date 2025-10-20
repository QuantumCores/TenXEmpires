using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.Repositories;

/// <summary>
/// Repository for Turn entity with turn-specific queries
/// </summary>
public interface ITurnRepository : IRepository<Turn>
{
    /// <summary>
    /// Get all turns for a game
    /// </summary>
    Task<IEnumerable<Turn>> GetGameTurnsAsync(long gameId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get latest turn for a game
    /// </summary>
    Task<Turn?> GetLatestTurnAsync(long gameId, CancellationToken cancellationToken = default);
}

