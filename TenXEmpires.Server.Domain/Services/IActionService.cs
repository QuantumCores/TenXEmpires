using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Service for game action commands (move, attack, etc.).
/// </summary>
public interface IActionService
{
    /// <summary>
    /// Moves a unit to a target position using deterministic pathfinding.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="command">The move command with unit ID and target position.</param>
    /// <param name="idempotencyKey">Optional idempotency key to prevent duplicate moves.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated game state after the move.</returns>
    /// <exception cref="InvalidOperationException">Thrown when move is invalid (not player's turn, unit not found, destination occupied).</exception>
    /// <exception cref="ArgumentException">Thrown when the move is illegal (path blocked, out of range).</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when user doesn't have access to the game.</exception>
    Task<ActionStateResponse> MoveUnitAsync(
        Guid userId,
        long gameId,
        MoveUnitCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}

