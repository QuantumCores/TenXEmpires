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

    /// <summary>
    /// Lists all saves for a specific game, grouped into manual saves and autosaves.
    /// </summary>
    /// <param name="gameId">The game ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of manual saves and autosaves.</returns>
    Task<GameSavesListDto> ListSavesAsync(long gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or overwrites a manual save in a specific slot for the current game turn and returns metadata.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="command">The create manual save command with slot and name.</param>
    /// <param name="idempotencyKey">Optional idempotency key to safely retry requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created/overwritten manual save metadata.</returns>
    Task<SaveCreatedDto> CreateManualAsync(
        Guid userId,
        long gameId,
        CreateManualSaveCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a manual save in the specified slot for the given game.
    /// Returns true if a save was deleted; false if not found.
    /// </summary>
    /// <param name="gameId">The game ID.</param>
    /// <param name="slot">The manual save slot (1..3).</param>
    /// <param name="idempotencyKey">Optional idempotency key to safely retry requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> DeleteManualAsync(
        long gameId,
        int slot,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}
