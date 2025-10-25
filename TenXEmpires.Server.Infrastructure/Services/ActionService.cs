using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Domain.Utilities;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Infrastructure.Services;

/// <summary>
/// Service implementation for game action commands (move, attack, etc.).
/// </summary>
public class ActionService : IActionService
{
    private readonly TenXDbContext _context;
    private readonly IGameStateService _gameStateService;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<ActionService> _logger;

    public ActionService(
        TenXDbContext context,
        IGameStateService gameStateService,
        IIdempotencyStore idempotencyStore,
        ILogger<ActionService> logger)
    {
        _context = context;
        _gameStateService = gameStateService;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
    }

    public async Task<ActionStateResponse> MoveUnitAsync(
        Guid userId,
        long gameId,
        MoveUnitCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        // Check idempotency if key provided
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cachedKey = CacheKeys.MoveUnitIdempotency(gameId, idempotencyKey);
            var cachedResponse = await _idempotencyStore.TryGetAsync<ActionStateResponse>(
                cachedKey,
                cancellationToken);

            if (cachedResponse != null)
            {
                _logger.LogInformation(
                    "Returning cached move response for idempotency key {IdempotencyKey} (game {GameId}, unit {UnitId})",
                    idempotencyKey,
                    gameId,
                    command.UnitId);
                return cachedResponse;
            }
        }

        // Begin serializable transaction to prevent race conditions
        await using var transaction = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            // Load game with necessary related entities
            var game = await _context.Games
                .Include(g => g.Map)
                .Include(g => g.ActiveParticipant)
                .FirstOrDefaultAsync(g => g.Id == gameId && g.UserId == userId, cancellationToken);

            if (game == null)
            {
                throw new UnauthorizedAccessException(
                    $"Game {gameId} not found or you don't have access to it.");
            }

            // Validate it is the human player's turn
            if (game.ActiveParticipant == null)
            {
                throw new InvalidOperationException("No active participant found for this game.");
            }

            if (game.ActiveParticipant.Kind != ParticipantKind.Human)
            {
                throw new InvalidOperationException("NOT_PLAYER_TURN: It is not your turn to move.");
            }

            if (game.ActiveParticipant.UserId != userId)
            {
                throw new InvalidOperationException("NOT_PLAYER_TURN: It is not your turn to move.");
            }

            // Check if turn is already in progress (guard against concurrent requests)
            if (game.TurnInProgress)
            {
                throw new InvalidOperationException(
                    "A turn action is already in progress. Please wait for it to complete.");
            }

            // Set turn in progress as a guard
            game.TurnInProgress = true;
            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                // Load the unit with its type definition and current tile
                var unit = await _context.Units
                    .Include(u => u.Type)
                    .Include(u => u.Tile)
                    .FirstOrDefaultAsync(u => u.Id == command.UnitId && u.GameId == gameId, cancellationToken);

                if (unit == null)
                {
                    throw new InvalidOperationException(
                        $"Unit {command.UnitId} not found in game {gameId}.");
                }

                // Verify unit belongs to active participant
                if (unit.ParticipantId != game.ActiveParticipantId)
                {
                    throw new InvalidOperationException(
                        "This unit does not belong to the active participant.");
                }

                // Check if unit has already acted this turn
                if (unit.HasActed)
                {
                    throw new InvalidOperationException(
                        "NO_ACTIONS_LEFT: This unit has already acted this turn.");
                }

                // Validate target position is within map bounds
                if (command.To.Row < 0 || command.To.Row >= game.Map.Height ||
                    command.To.Col < 0 || command.To.Col >= game.Map.Width)
                {
                    throw new ArgumentException(
                        $"Target position ({command.To.Row}, {command.To.Col}) is out of map bounds.");
                }

                // Get the target tile
                var targetTile = await _context.MapTiles
                    .FirstOrDefaultAsync(
                        t => t.MapId == game.MapId &&
                             t.Row == command.To.Row &&
                             t.Col == command.To.Col,
                        cancellationToken);

                if (targetTile == null)
                {
                    throw new ArgumentException(
                        $"Target tile at position ({command.To.Row}, {command.To.Col}) not found.");
                }

                // Check if destination is already occupied by another unit (1UPT constraint)
                var occupyingUnit = await _context.Units
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        u => u.GameId == gameId && u.TileId == targetTile.Id && u.Id != unit.Id,
                        cancellationToken);

                if (occupyingUnit != null)
                {
                    throw new InvalidOperationException(
                        $"ONE_UNIT_PER_TILE: The destination tile at ({command.To.Row}, {command.To.Col}) is already occupied by another unit.");
                }

                // Load all units for blocking check
                var allUnits = await _context.Units
                    .Include(u => u.Tile)
                    .Where(u => u.GameId == gameId && u.Id != unit.Id)
                    .Select(u => new { u.Tile.Row, u.Tile.Col })
                    .ToListAsync(cancellationToken);

                var occupiedPositions = allUnits
                    .Select(u => (u.Row, u.Col))
                    .ToHashSet();

                // Create blocking function for pathfinding
                bool IsBlocked(GridPosition pos)
                {
                    return occupiedPositions.Contains((pos.Row, pos.Col));
                }

                // Calculate path using A* pathfinding
                var currentPosition = new GridPosition(unit.Tile.Row, unit.Tile.Col);
                var targetPosition = command.To;

                var path = PathfindingHelper.FindPath(
                    currentPosition,
                    targetPosition,
                    unit.Type.MovePoints,
                    game.Map.Width,
                    game.Map.Height,
                    IsBlocked);

                if (path == null)
                {
                    throw new ArgumentException(
                        $"ILLEGAL_MOVE: No valid path found from ({currentPosition.Row}, {currentPosition.Col}) " +
                        $"to ({targetPosition.Row}, {targetPosition.Col}) within {unit.Type.MovePoints} movement points.");
                }

                // Update unit position and mark as acted
                unit.TileId = targetTile.Id;
                unit.HasActed = true;
                unit.UpdatedAt = DateTimeOffset.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Unit {UnitId} moved from ({FromRow}, {FromCol}) to ({ToRow}, {ToCol}) in game {GameId}",
                    unit.Id,
                    currentPosition.Row,
                    currentPosition.Col,
                    targetPosition.Row,
                    targetPosition.Col,
                    gameId);

                // Clear turn in progress flag
                game.TurnInProgress = false;
                await _context.SaveChangesAsync(cancellationToken);

                // Commit transaction
                await transaction.CommitAsync(cancellationToken);

                // Build updated game state
                var gameState = await _gameStateService.BuildGameStateAsync(gameId, cancellationToken);
                var response = new ActionStateResponse(gameState);

                // Store in idempotency cache if key provided
                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var cachedKey = CacheKeys.MoveUnitIdempotency(gameId, idempotencyKey);
                    await _idempotencyStore.TryStoreAsync(
                        cachedKey,
                        response,
                        TimeSpan.FromHours(1),
                        cancellationToken);
                }

                return response;
            }
            catch
            {
                // Clear turn in progress flag on error
                game.TurnInProgress = false;
                await _context.SaveChangesAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            
            _logger.LogError(
                ex,
                "Failed to move unit {UnitId} in game {GameId}",
                command.UnitId,
                gameId);
            
            throw;
        }
    }

    public async Task<ActionStateResponse> AttackAsync(
        Guid userId,
        long gameId,
        AttackUnitCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        // Check idempotency if key provided
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cachedKey = CacheKeys.AttackUnitIdempotency(gameId, idempotencyKey);
            var cachedResponse = await _idempotencyStore.TryGetAsync<ActionStateResponse>(
                cachedKey,
                cancellationToken);

            if (cachedResponse != null)
            {
                _logger.LogInformation(
                    "Returning cached attack response for idempotency key {IdempotencyKey} (game {GameId}, attacker {AttackerId}, target {TargetId})",
                    idempotencyKey,
                    gameId,
                    command.AttackerUnitId,
                    command.TargetUnitId);
                return cachedResponse;
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            // Load game and active participant
            var game = await _context.Games
                .Include(g => g.Map)
                .Include(g => g.ActiveParticipant)
                .FirstOrDefaultAsync(g => g.Id == gameId && g.UserId == userId, cancellationToken);

            if (game == null)
            {
                throw new UnauthorizedAccessException(
                    $"Game {gameId} not found or you don't have access to it.");
            }

            if (game.ActiveParticipant == null)
            {
                throw new InvalidOperationException("No active participant found for this game.");
            }

            if (game.ActiveParticipant.Kind != ParticipantKind.Human || game.ActiveParticipant.UserId != userId)
            {
                throw new InvalidOperationException("NOT_PLAYER_TURN: It is not your turn to act.");
            }

            if (game.TurnInProgress)
            {
                throw new InvalidOperationException(
                    "A turn action is already in progress. Please wait for it to complete.");
            }

            // Guard
            game.TurnInProgress = true;
            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                // Load attacker and target units with types and tiles
                var attacker = await _context.Units
                    .Include(u => u.Type)
                    .Include(u => u.Tile)
                    .FirstOrDefaultAsync(u => u.Id == command.AttackerUnitId && u.GameId == gameId, cancellationToken);

                if (attacker == null)
                {
                    throw new InvalidOperationException($"UNIT_NOT_FOUND: Attacker unit {command.AttackerUnitId} not found in game {gameId}.");
                }

                if (attacker.ParticipantId != game.ActiveParticipantId)
                {
                    throw new InvalidOperationException("This unit does not belong to the active participant.");
                }

                if (attacker.HasActed)
                {
                    throw new InvalidOperationException("NO_ACTIONS_LEFT: This unit has already acted this turn.");
                }

                var target = await _context.Units
                    .Include(u => u.Type)
                    .Include(u => u.Tile)
                    .FirstOrDefaultAsync(u => u.Id == command.TargetUnitId && u.GameId == gameId, cancellationToken);

                if (target == null)
                {
                    throw new InvalidOperationException($"UNIT_NOT_FOUND: Target unit {command.TargetUnitId} not found in game {gameId}.");
                }

                if (target.ParticipantId == attacker.ParticipantId)
                {
                    throw new ArgumentException("INVALID_TARGET: Target must be an enemy unit.");
                }

                // Range validation using hex distance
                var attackerCube = Domain.Utilities.HexagonalGrid.ConvertOddrToCube(attacker.Tile.Col, attacker.Tile.Row);
                var targetCube = Domain.Utilities.HexagonalGrid.ConvertOddrToCube(target.Tile.Col, target.Tile.Row);
                var distance = Domain.Utilities.HexagonalGrid.GetCubeDistance(attackerCube, targetCube);

                if (!attacker.Type.IsRanged)
                {
                    // Melee must be adjacent
                    if (distance != 1)
                    {
                        throw new ArgumentException("OUT_OF_RANGE: Melee attacks require adjacency.");
                    }
                }
                else
                {
                    var min = attacker.Type.RangeMin;
                    var max = attacker.Type.RangeMax;
                    if (distance < min || distance > max)
                    {
                        throw new ArgumentException($"OUT_OF_RANGE: Ranged attack distance {distance} not in [{min},{max}].");
                    }
                }

                // Compute damage using HP-adapted stats:
                // effectiveAtk = Attack * (attackerHp / attackerMaxHp)
                // effectiveDef = Defence * (defenderHp / defenderMaxHp)
                // DMG = effectiveAtk * (1 + (effectiveAtk - effectiveDef)/effectiveDef) * 0.5
                static int ComputeDamage(
                    int atkStat,
                    int defStat,
                    int attackerHp,
                    int attackerMaxHp,
                    int defenderHp,
                    int defenderMaxHp)
                {
                    var atkRatio = attackerMaxHp > 0 ? Math.Clamp(attackerHp / (double)attackerMaxHp, 0.0, 1.0) : 1.0;
                    var defRatio = defenderMaxHp > 0 ? Math.Clamp(defenderHp / (double)defenderMaxHp, 0.0, 1.0) : 1.0;
                    var atk = atkStat * atkRatio;
                    var def = defStat * defRatio;
                    if (def <= 0) def = 1; // safety
                    var value = (atk * (1.0 + (atk - def) / def) * 0.5);
                    var rounded = (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);
                    return Math.Max(1, rounded);
                }

                var attackerDamage = ComputeDamage(
                    attacker.Type.Attack,
                    target.Type.Defence,
                    attacker.Hp,
                    attacker.Type.Health,
                    target.Hp,
                    target.Type.Health);

                // Apply attacker damage first
                target.Hp -= attackerDamage;
                target.UpdatedAt = DateTimeOffset.UtcNow;

                if (target.Hp > 0)
                {
                    // Counterattack only if defender survives and is eligible (melee defender; attacker is melee)
                    var defenderCanCounter = !target.Type.IsRanged; // ranged defenders never counterattack
                    var attackerReceivesCounter = !attacker.Type.IsRanged; // ranged attackers never receive counterattack

                    if (defenderCanCounter && attackerReceivesCounter)
                    {
                        var counterDamage = ComputeDamage(
                            target.Type.Attack,
                            attacker.Type.Defence,
                            target.Hp,
                            target.Type.Health,
                            attacker.Hp,
                            attacker.Type.Health);
                        attacker.Hp -= counterDamage;
                        attacker.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                }

                // Note: No special tie handling; if both reach <= 0 HP, both are removed.

                // Remove destroyed units
                if (target.Hp <= 0)
                {
                    _context.Units.Remove(target);
                }

                if (attacker.Hp <= 0)
                {
                    _context.Units.Remove(attacker);
                }

                // Mark attacker as acted
                if (_context.Entry(attacker).State != EntityState.Deleted)
                {
                    attacker.HasActed = true;
                    attacker.UpdatedAt = DateTimeOffset.UtcNow;
                }

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Attack resolved in game {GameId}: attacker {AttackerId} -> target {TargetId}, dmg={Damage}, dist={Distance}",
                    gameId, attacker.Id, target.Id, attackerDamage, distance);

                // Clear guard
                game.TurnInProgress = false;
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                var gameState = await _gameStateService.BuildGameStateAsync(gameId, cancellationToken);
                var response = new ActionStateResponse(gameState);

                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var cacheKey = CacheKeys.AttackUnitIdempotency(gameId, idempotencyKey);
                    await _idempotencyStore.TryStoreAsync(cacheKey, response, TimeSpan.FromHours(1), cancellationToken);
                }

                return response;
            }
            catch
            {
                // Clear guard on error
                game.TurnInProgress = false;
                await _context.SaveChangesAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex,
                "Failed to execute attack {AttackerId}->{TargetId} in game {GameId}",
                command.AttackerUnitId,
                command.TargetUnitId,
                gameId);
            throw;
        }
    }
}

