using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Service for game-related business logic and queries.
/// </summary>
public interface IGameService
{
    /// <summary>
    /// Lists games for the authenticated user with filtering, sorting, and pagination.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="query">Query parameters for filtering, sorting, and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paged result containing game list items.</returns>
    Task<PagedResult<GameListItemDto>> ListGamesAsync(
        Guid userId,
        ListGamesQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new game for the authenticated user with initial state.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="command">The command containing map code and optional settings.</param>
    /// <param name="idempotencyKey">Optional idempotency key to prevent duplicate game creation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created game with its initial state.</returns>
    /// <exception cref="InvalidOperationException">Thrown when map is not found, schema mismatch, or game limit reached.</exception>
    /// <exception cref="ArgumentException">Thrown when input parameters are invalid.</exception>
    Task<GameCreatedResponse> CreateGameAsync(
        Guid userId,
        CreateGameCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information for a specific game owned by the authenticated user.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="gameId">The game ID to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The detailed game DTO if found; otherwise null.</returns>
    Task<GameDetailDto?> GetGameDetailAsync(
        Guid userId,
        long gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that a user has access to a specific game.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="gameId">The game ID to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user has access to the game, false otherwise.</returns>
    Task<bool> VerifyGameAccessAsync(
        Guid userId,
        long gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a game and all associated child entities (participants, units, cities, saves, turns) 
    /// owned by the authenticated user.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="gameId">The game ID to delete.</param>
    /// <param name="idempotencyKey">Optional idempotency key to prevent duplicate deletions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the game was deleted, false if not found or not accessible.</returns>
    Task<bool> DeleteGameAsync(
        Guid userId,
        long gameId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}

