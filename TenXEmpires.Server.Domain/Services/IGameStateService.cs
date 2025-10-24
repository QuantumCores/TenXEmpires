using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Service for building game state projections.
/// </summary>
public interface IGameStateService
{
    /// <summary>
    /// Builds the complete game state DTO for a game.
    /// </summary>
    /// <param name="gameId">The game ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete game state including all entities.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the game is not found.</exception>
    Task<GameStateDto> BuildGameStateAsync(
        long gameId,
        CancellationToken cancellationToken = default);
}

