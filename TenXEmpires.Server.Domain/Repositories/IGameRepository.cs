using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.Repositories;

/// <summary>
/// Repository for Game entity with game-specific queries
/// </summary>
public interface IGameRepository : IRepository<Game>
{
    /// <summary>
    /// Get all games for a specific user
    /// </summary>
    Task<IEnumerable<Game>> GetUserGamesAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get game with all related data (participants, units, cities)
    /// </summary>
    Task<Game?> GetGameWithDetailsAsync(long gameId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get active (non-finished) games for a user
    /// </summary>
    Task<IEnumerable<Game>> GetActiveUserGamesAsync(Guid userId, CancellationToken cancellationToken = default);
}

