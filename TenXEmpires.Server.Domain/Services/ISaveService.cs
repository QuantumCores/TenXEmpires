using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Service for creating and managing game saves.
/// </summary>
public interface ISaveService
{
    /// <summary>
    /// Creates an autosave for the specified game and enforces a ring buffer of the most recent 5 autosaves.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="turnNo">The current turn number.</param>
    /// <param name="activeParticipantId">The current active participant ID.</param>
    /// <param name="schemaVersion">The map schema version.</param>
    /// <param name="mapCode">The map code.</param>
    /// <param name="state">The game state snapshot to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created autosave ID.</returns>
    Task<long> CreateAutosaveAsync(
        Guid userId,
        long gameId,
        int turnNo,
        long activeParticipantId,
        int schemaVersion,
        string mapCode,
        GameStateDto state,
        CancellationToken cancellationToken = default);
}

