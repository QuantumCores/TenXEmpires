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

    /// <summary>
    /// Executes an attack from one unit to another with deterministic damage.
    /// Ranged attackers never receive counterattacks.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="command">The attack command with attacker and target IDs.</param>
    /// <param name="idempotencyKey">Optional idempotency key for safe retries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated game state after the attack.</returns>
    /// <exception cref="InvalidOperationException">Thrown when attack is invalid (not player's turn, unit not found, already acted).</exception>
    /// <exception cref="ArgumentException">Thrown when the attack is illegal (out of range, invalid target).</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when user doesn't have access to the game.</exception>
    Task<ActionStateResponse> AttackAsync(
        Guid userId,
        long gameId,
        AttackUnitCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an attack from a unit against an enemy city using deterministic damage.
    /// City does not counterattack. Returns updated game state.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="attackerUnitId">The attacking unit ID.</param>
    /// <param name="targetCityId">The target city ID.</param>
    /// <param name="idempotencyKey">Optional idempotency key for safe retries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ActionStateResponse> AttackCityAsync(
        Guid userId,
        long gameId,
        long attackerUnitId,
        long targetCityId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}

