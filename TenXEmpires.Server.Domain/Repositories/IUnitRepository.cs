using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.Repositories;

/// <summary>
/// Repository for Unit entity with unit-specific queries
/// </summary>
public interface IUnitRepository : IRepository<Unit>
{
    /// <summary>
    /// Get all units in a game
    /// </summary>
    Task<IEnumerable<Unit>> GetGameUnitsAsync(long gameId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all units for a participant
    /// </summary>
    Task<IEnumerable<Unit>> GetParticipantUnitsAsync(long participantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get unit on a specific tile (1UPT constraint)
    /// </summary>
    Task<Unit?> GetUnitOnTileAsync(long gameId, long tileId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get units that haven't acted this turn
    /// </summary>
    Task<IEnumerable<Unit>> GetUnitsReadyToActAsync(long participantId, CancellationToken cancellationToken = default);
}

