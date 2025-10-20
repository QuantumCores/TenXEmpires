using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.Repositories;

/// <summary>
/// Repository for Participant entity with participant-specific queries
/// </summary>
public interface IParticipantRepository : IRepository<Participant>
{
    /// <summary>
    /// Get all participants in a game
    /// </summary>
    Task<IEnumerable<Participant>> GetGameParticipantsAsync(long gameId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get human participant for a user in a game
    /// </summary>
    Task<Participant?> GetUserParticipantAsync(long gameId, Guid userId, CancellationToken cancellationToken = default);
}

