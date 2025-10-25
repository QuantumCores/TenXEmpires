using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Service for turn-related business logic and queries.
/// </summary>
public interface ITurnService
{
    /// <summary>
    /// Lists turns for a specific game with sorting and pagination.
    /// </summary>
    /// <param name="gameId">The game ID to list turns for.</param>
    /// <param name="query">Query parameters for sorting and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paged result containing turn DTOs.</returns>
    /// <exception cref="ArgumentException">Thrown when sort or order parameters are invalid.</exception>
    Task<PagedResult<TurnDto>> ListTurnsAsync(
        long gameId,
        ListTurnsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the active participant's turn, commits a Turn row, creates an autosave, and advances to the next participant.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="command">Optional command payload (currently empty).</param>
    /// <param name="idempotencyKey">Optional idempotency key for safe retries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>End turn response including updated state, turn summary and autosave id.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not player's turn or a turn is already in progress.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when user doesn't have access to the game.</exception>
    Task<EndTurnResponse> EndTurnAsync(
        Guid userId,
        long gameId,
        EndTurnCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}

