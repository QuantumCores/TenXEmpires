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
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
            transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // InMemory provider: transactions not supported; proceed without explicit transaction
        }
        catch (NotSupportedException)
        {
            // Non-relational provider; proceed without explicit transaction
        }

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

                // Load map tiles to check for water/ocean terrain
                var mapTiles = await _context.MapTiles
                    .AsNoTracking()
                    .Where(t => t.MapId == game.MapId)
                    .Select(t => new { t.Row, t.Col, t.Terrain })
                    .ToListAsync(cancellationToken);

                var waterTiles = mapTiles
                    .Where(t => TerrainTypes.IsWater(t.Terrain))
                    .Select(t => (t.Row, t.Col))
                    .ToHashSet();

                // Create blocking function for pathfinding
                bool IsBlocked(GridPosition pos)
                {
                    // Block if occupied by another unit
                    if (occupiedPositions.Contains((pos.Row, pos.Col)))
                    {
                        return true;
                    }
                    // Block if tile is water or ocean
                    if (waterTiles.Contains((pos.Row, pos.Col)))
                    {
                        return true;
                    }
                    return false;
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

                // If melee unit ends turn on a defeated enemy city tile, capture it
                var cityOnTile = await _context.Cities
                    .Include(c => c.Tile)
                    .FirstOrDefaultAsync(c => c.GameId == gameId && c.TileId == targetTile.Id, cancellationToken);
                if (cityOnTile != null && cityOnTile.ParticipantId != unit.ParticipantId && cityOnTile.Hp <= 0 && !unit.Type.IsRanged)
                {
                    await CityCaptureHelper.CaptureCityAsync(_context, cityOnTile, unit.ParticipantId, cancellationToken);
                }

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
                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

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
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            
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

                // Resolve attack using shared combat helper (applies counter and HP updates)
                var outcome = CombatHelper.ResolveAttack(attacker, target);

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
                    gameId, attacker.Id, target.Id, outcome.AttackerDamage, distance);

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
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogError(ex,
                "Failed to execute attack {AttackerId}->{TargetId} in game {GameId}",
                command.AttackerUnitId,
                command.TargetUnitId,
                gameId);
            throw;
        }
    }

    public async Task<ActionStateResponse> AttackCityAsync(
        Guid userId,
        long gameId,
        long attackerUnitId,
        long targetCityId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        // Check idempotency if key provided
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cachedKey = CacheKeys.AttackCityIdempotency(gameId, idempotencyKey);
            var cachedResponse = await _idempotencyStore.TryGetAsync<ActionStateResponse>(
                cachedKey,
                cancellationToken);

            if (cachedResponse != null)
            {
                _logger.LogInformation(
                    "Returning cached city attack response for idempotency key {IdempotencyKey} (game {GameId}, attacker {AttackerId}, city {CityId})",
                    idempotencyKey,
                    gameId,
                    attackerUnitId,
                    targetCityId);
                return cachedResponse;
            }
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
            transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);
        }
        catch (InvalidOperationException) { }
        catch (NotSupportedException) { }

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
                // Load attacker and target city with tiles
                var attacker = await _context.Units
                    .Include(u => u.Type)
                    .Include(u => u.Tile)
                    .FirstOrDefaultAsync(u => u.Id == attackerUnitId && u.GameId == gameId, cancellationToken);

                if (attacker == null)
                {
                    throw new InvalidOperationException($"UNIT_NOT_FOUND: Attacker unit {attackerUnitId} not found in game {gameId}.");
                }

                if (attacker.ParticipantId != game.ActiveParticipantId)
                {
                    throw new InvalidOperationException("This unit does not belong to the active participant.");
                }

                if (attacker.HasActed)
                {
                    throw new InvalidOperationException("NO_ACTIONS_LEFT: This unit has already acted this turn.");
                }

                var city = await _context.Cities
                    .Include(c => c.Tile)
                    .FirstOrDefaultAsync(c => c.Id == targetCityId && c.GameId == gameId, cancellationToken);

                if (city == null)
                {
                    throw new InvalidOperationException($"CITY_NOT_FOUND: City {targetCityId} not found in game {gameId}.");
                }

                if (city.ParticipantId == attacker.ParticipantId)
                {
                    throw new ArgumentException("INVALID_TARGET: Target must be an enemy city.");
                }

                // Range validation using hex distance
                var attackerCube = Domain.Utilities.HexagonalGrid.ConvertOddrToCube(attacker.Tile.Col, attacker.Tile.Row);
                var cityCube = Domain.Utilities.HexagonalGrid.ConvertOddrToCube(city.Tile.Col, city.Tile.Row);
                var distance = Domain.Utilities.HexagonalGrid.GetCubeDistance(attackerCube, cityCube);

                if (!attacker.Type.IsRanged)
                {
                    if (distance != 1)
                    {
                        throw new ArgumentException("OUT_OF_RANGE: Melee attacks on city require adjacency.");
                    }
                }
                else
                {
                    var min = attacker.Type.RangeMin;
                    var max = attacker.Type.RangeMax;
                    if (distance < min || distance > max)
                    {
                        throw new ArgumentException($"OUT_OF_RANGE: Ranged attack distance {distance} not in [{min},{max}] for city.");
                    }
                }

                // Resolve attack on city
                var dmg = CombatHelper.ResolveAttackOnCity(attacker, city);
                var cityDefeated = city.Hp <= 0;

                // Attacker marks as acted (never receives counterattack from city)
                attacker.HasActed = true;
                attacker.UpdatedAt = DateTimeOffset.UtcNow;

                if (cityDefeated)
                {
                    await CityCaptureHelper.CaptureCityAsync(_context, city, attacker.ParticipantId, cancellationToken);
                    _logger.LogInformation(
                        "City {CityId} captured by participant {ParticipantId} after attack in game {GameId}",
                        city.Id,
                        attacker.ParticipantId,
                        gameId);
                }

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "City attack resolved in game {GameId}: attacker {AttackerId} -> city {CityId}, dmg={Damage}, dist={Distance}",
                    gameId, attacker.Id, city.Id, dmg, distance);

                // Clear guard
                game.TurnInProgress = false;
                await _context.SaveChangesAsync(cancellationToken);

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                var gameState = await _gameStateService.BuildGameStateAsync(gameId, cancellationToken);
                var response = new ActionStateResponse(gameState);

                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var cacheKey = CacheKeys.AttackCityIdempotency(gameId, idempotencyKey);
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
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogError(ex,
                "Failed to execute city attack {AttackerId}->{CityId} in game {GameId}",
                attackerUnitId,
                targetCityId,
                gameId);
            throw;
        }
    }

    public async Task<ActionStateResponse> SpawnUnitAsync(
        Guid userId,
        long gameId,
        SpawnUnitCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cachedKey = CacheKeys.SpawnUnitIdempotency(gameId, idempotencyKey);
            var cached = await _idempotencyStore.TryGetAsync<ActionStateResponse>(cachedKey, cancellationToken);
            if (cached is not null)
            {
                _logger.LogInformation(
                    "Returning cached spawn response for key {IdempotencyKey} (game {GameId}, city {CityId})",
                    idempotencyKey,
                    gameId,
                    command.CityId);
                return cached;
            }
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
            transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);
        }
        catch (InvalidOperationException) { }
        catch (NotSupportedException) { }

        try
        {
            var game = await _context.Games
                .Include(g => g.Map)
                .Include(g => g.ActiveParticipant)
                .FirstOrDefaultAsync(g => g.Id == gameId && g.UserId == userId, cancellationToken);

            if (game is null)
            {
                throw new UnauthorizedAccessException($"Game {gameId} not found or you don't have access to it.");
            }

            if (game.ActiveParticipant is null)
            {
                throw new InvalidOperationException("No active participant found for this game.");
            }

            if (game.ActiveParticipant.Kind != ParticipantKind.Human || game.ActiveParticipant.UserId != userId)
            {
                throw new InvalidOperationException("NOT_PLAYER_TURN: It is not your turn to act.");
            }

            if (game.TurnInProgress)
            {
                throw new InvalidOperationException("TURN_IN_PROGRESS: A turn action is already in progress. Please wait.");
            }

            // Guard against concurrent actions
            game.TurnInProgress = true;
            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                var normalizedCode = command.UnitCode?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(normalizedCode) || !UnitTypes.ValidTypes.Contains(normalizedCode))
                {
                    throw new ArgumentException("INVALID_UNIT: Unsupported unit type.");
                }

                var city = await _context.Cities
                    .Include(c => c.Tile)
                    .Include(c => c.CityResources)
                    .FirstOrDefaultAsync(c => c.Id == command.CityId && c.GameId == gameId, cancellationToken);

                if (city is null || city.ParticipantId != game.ActiveParticipantId)
                {
                    throw new InvalidOperationException("CITY_NOT_FOUND_OR_OWNED: City not found or not owned by the active participant.");
                }

                if (city.HasActedThisTurn)
                {
                    throw new InvalidOperationException("CITY_ALREADY_ACTED: This city has already acted this turn.");
                }

                var unitDef = await _context.UnitDefinitions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ud => ud.Code == normalizedCode, cancellationToken);

                if (unitDef is null)
                {
                    throw new ArgumentException($"INVALID_UNIT: Unit definition '{normalizedCode}' not found.");
                }

                var (costResource, costAmount) = normalizedCode switch
                {
                    UnitTypes.Warrior => (ResourceTypes.Iron, 10),
                    UnitTypes.Slinger => (ResourceTypes.Stone, 10),
                    _ => throw new ArgumentException("INVALID_UNIT: Unsupported unit type.")
                };

                var cr = city.CityResources.FirstOrDefault(r => r.ResourceType == costResource);
                var currentAmount = cr?.Amount ?? 0;
                if (currentAmount < costAmount)
                {
                    throw new InvalidOperationException(
                        $"INSUFFICIENT_RESOURCES: Requires {costAmount} {costResource}, city has {currentAmount}.");
                }

                // Load occupancy and map tiles
                var units = await _context.Units
                    .Include(u => u.Tile)
                    .Where(u => u.GameId == gameId)
                    .ToListAsync(cancellationToken);

                var occupiedTiles = units.Select(u => u.TileId).ToHashSet();

                var mapTiles = await _context.MapTiles
                    .AsNoTracking()
                    .Where(t => t.MapId == game.MapId)
                    .ToDictionaryAsync(t => (t.Row, t.Col), t => t, cancellationToken);
                var cityTileOwners = await _context.Cities
                    .AsNoTracking()
                    .Where(c => c.GameId == gameId)
                    .ToDictionaryAsync(c => c.TileId, c => c.Id, cancellationToken);

                bool IsTileAvailable(MapTile tile)
                {
                    if (TerrainTypes.IsWater(tile.Terrain))
                    {
                        return false;
                    }

                    if (occupiedTiles.Contains(tile.Id))
                    {
                        return false;
                    }

                    if (cityTileOwners.TryGetValue(tile.Id, out var ownerCityId) && ownerCityId != city.Id)
                    {
                        return false;
                    }

                    return true;
                }

                MapTile? spawnTile = null;

                // Prefer spawning on the city tile if free
                if (mapTiles.TryGetValue((city.Tile.Row, city.Tile.Col), out var cityTile) && IsTileAvailable(cityTile))
                {
                    spawnTile = cityTile;
                }

                if (spawnTile is null)
                {
                    var centerCube = HexagonalGrid.ConvertOddrToCube(city.Tile.Col, city.Tile.Row);
                    foreach (var dir in HexagonalGrid.CubeDirections)
                    {
                        var neighborCube = centerCube + dir;
                        var neighborSq = HexagonalGrid.ConvertCubeToOddr(neighborCube);

                        if (neighborSq.Y < 0 || neighborSq.Y >= game.Map.Height ||
                            neighborSq.X < 0 || neighborSq.X >= game.Map.Width)
                        {
                            continue;
                        }

                        if (!mapTiles.TryGetValue((neighborSq.Y, neighborSq.X), out var neighborTile))
                        {
                            continue;
                        }

                        if (IsTileAvailable(neighborTile))
                        {
                            spawnTile = neighborTile;
                            break;
                        }
                    }
                }

                if (spawnTile is null)
                {
                    throw new InvalidOperationException("SPAWN_BLOCKED: No free adjacent tile to place the unit.");
                }

                var newUnit = new Unit
                {
                    GameId = gameId,
                    ParticipantId = city.ParticipantId,
                    TypeId = unitDef.Id,
                    TileId = spawnTile.Id,
                    Hp = unitDef.Health,
                    HasActed = false,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                _context.Units.Add(newUnit);
                if (cr is null)
                {
                    cr = new CityResource { CityId = city.Id, ResourceType = costResource, Amount = 0 };
                    city.CityResources.Add(cr);
                    _context.CityResources.Add(cr);
                }
                cr.Amount -= costAmount;
                city.HasActedThisTurn = true;

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Spawned {UnitCode} from city {CityId} on tile {TileId} in game {GameId}",
                    normalizedCode,
                    city.Id,
                    spawnTile.Id,
                    gameId);

                // Clear guard
                game.TurnInProgress = false;
                await _context.SaveChangesAsync(cancellationToken);

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                var gameState = await _gameStateService.BuildGameStateAsync(gameId, cancellationToken);
                var response = new ActionStateResponse(gameState);

                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var cacheKey = CacheKeys.SpawnUnitIdempotency(gameId, idempotencyKey);
                    await _idempotencyStore.TryStoreAsync(cacheKey, response, TimeSpan.FromHours(1), cancellationToken);
                }

                return response;
            }
            catch
            {
                game.TurnInProgress = false;
                await _context.SaveChangesAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            _logger.LogError(ex, "Failed to spawn unit in game {GameId} from city {CityId}", gameId, command.CityId);
            throw;
        }
    }

    public async Task<ActionStateResponse> ExpandTerritoryAsync(
        Guid userId,
        long gameId,
        ExpandTerritoryCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cachedKey = CacheKeys.SpawnUnitIdempotency(gameId, idempotencyKey); // Reusing prefix pattern but should be unique for expand or general action
            // Ideally CacheKeys should have ExpandTerritoryIdempotency or generic ActionIdempotency
            // For now using a unique suffix in the key composition if possible or just string interpolation
             var actualKey = $"game:{gameId}:expand:{idempotencyKey}";
            var cached = await _idempotencyStore.TryGetAsync<ActionStateResponse>(actualKey, cancellationToken);
            if (cached is not null)
            {
                return cached;
            }
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
            transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);
        }
        catch (InvalidOperationException) { }
        catch (NotSupportedException) { }

        try
        {
            var game = await _context.Games
                .Include(g => g.Map)
                .Include(g => g.ActiveParticipant)
                .FirstOrDefaultAsync(g => g.Id == gameId && g.UserId == userId, cancellationToken);

            if (game is null) throw new UnauthorizedAccessException($"Game {gameId} not found or access denied.");
            if (game.ActiveParticipant is null) throw new InvalidOperationException("No active participant.");
            if (game.ActiveParticipant.Kind != ParticipantKind.Human || game.ActiveParticipant.UserId != userId)
                throw new InvalidOperationException("NOT_PLAYER_TURN: Not your turn.");
            if (game.TurnInProgress)
                throw new InvalidOperationException("TURN_IN_PROGRESS: Action in progress.");

            game.TurnInProgress = true;
            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                // Load city
                var city = await _context.Cities
                    .Include(c => c.CityResources)
                    .Include(c => c.Tile)
                    .FirstOrDefaultAsync(c => c.Id == command.CityId && c.GameId == gameId, cancellationToken);

                if (city is null || city.ParticipantId != game.ActiveParticipantId)
                    throw new InvalidOperationException("CITY_NOT_OWNED: City not owned by player.");

                if (city.HasActedThisTurn)
                    throw new InvalidOperationException("CITY_ALREADY_ACTED: City has already acted.");

                // Load target tile
                var targetTile = await _context.MapTiles
                    .FirstOrDefaultAsync(t => t.Id == command.TargetTileId && t.MapId == game.MapId, cancellationToken);

                if (targetTile is null)
                    throw new ArgumentException("INVALID_TILE: Target tile not found.");

                if (TerrainTypes.IsWater(targetTile.Terrain))
                    throw new InvalidOperationException("INVALID_TERRAIN: Cannot expand into water.");

                // Validate adjacency
                // Need all city tiles to check adjacency
                var cityTiles = await _context.CityTiles
                    .Include(ct => ct.Tile)
                    .Where(ct => ct.CityId == city.Id)
                    .ToListAsync(cancellationToken);

                var cityTileIds = cityTiles.Select(ct => ct.TileId).ToHashSet();
                if (cityTileIds.Contains(targetTile.Id))
                    throw new InvalidOperationException("TILE_ALREADY_OWNED: Tile already part of city.");

                var targetCube = HexagonalGrid.ConvertOddrToCube(targetTile.Col, targetTile.Row);
                bool isAdjacent = false;
                foreach (var ct in cityTiles)
                {
                    var ctCube = HexagonalGrid.ConvertOddrToCube(ct.Tile.Col, ct.Tile.Row);
                    if (HexagonalGrid.GetCubeDistance(ctCube, targetCube) == 1)
                    {
                        isAdjacent = true;
                        break;
                    }
                }

                if (!isAdjacent)
                    throw new InvalidOperationException("TILE_NOT_ADJACENT: Target tile not adjacent to territory.");

                // Validate distance from city center (max 2 hex)
                const int MaxExpansionDistance = 2;
                var cityCenterCube = HexagonalGrid.ConvertOddrToCube(city.Tile.Col, city.Tile.Row);
                var distanceFromCenter = HexagonalGrid.GetCubeDistance(cityCenterCube, targetCube);
                if (distanceFromCenter > MaxExpansionDistance)
                    throw new InvalidOperationException($"TILE_TOO_FAR: Target tile must be within {MaxExpansionDistance} hexes of city center.");

                // Check ownership by other cities
                var otherOwner = await _context.CityTiles
                    .Where(ct => ct.TileId == targetTile.Id)
                    .Select(ct => ct.City.ParticipantId)
                    .FirstOrDefaultAsync(cancellationToken);
                
                if (otherOwner != 0) // 0 is default if not found? No, FirstOrDefault returns 0 for long/int default?
                                     // Actually Select returns distinct values.
                                     // Let's check if any CityTile exists for this tile.
                {
                     var existingOwner = await _context.CityTiles
                        .Include(ct => ct.City)
                        .FirstOrDefaultAsync(ct => ct.TileId == targetTile.Id, cancellationToken);
                     
                     if (existingOwner != null)
                     {
                         if (existingOwner.City.ParticipantId != city.ParticipantId)
                             throw new InvalidOperationException("TILE_OWNED_BY_ENEMY: Tile owned by enemy.");
                         // Else it's ours, caught by previous check or valid?
                         // We already checked cityTileIds. If it belongs to another city of SAME player?
                         // The spec says "NOT owned by an enemy city".
                         // Implies we can steal from our own other cities? Or merge?
                         // For now assume strict "not owned by anyone else".
                         // Actually if it is owned by another city of same player, it is effectively "already owned" in broader sense
                         // but US says "NOT owned by an enemy city".
                         // Let's stick to implementation plan: "If owned by enemy city -> reject".
                         // If owned by same player's other city, technically allowed?
                         // But standard Civ logic usually prevents overlapping city tiles.
                         // Let's assume any ownership blocks expansion for simplicity unless specified.
                         throw new InvalidOperationException("TILE_ALREADY_OWNED: Tile already owned.");
                     }
                }

                // Check enemy unit occupation
                var enemyUnit = await _context.Units
                    .AnyAsync(u => u.GameId == gameId && u.TileId == targetTile.Id && u.ParticipantId != city.ParticipantId, cancellationToken);
                if (enemyUnit)
                    throw new InvalidOperationException("TILE_OCCUPIED_BY_ENEMY: Tile occupied by enemy unit.");

                // Calculate cost
                // Formula: BaseCost + ((ControlledTilesCount - InitialTilesCount) * 10)
                // We need config values. Hardcoding for now as per PRD defaults if not injected
                int baseCost = 20; // Should inject GameSettings
                int costPerTile = 10;
                int initialTiles = 7;
                
                int currentTileCount = cityTiles.Count;
                int extraTiles = Math.Max(0, currentTileCount - initialTiles);
                int cost = baseCost + (extraTiles * costPerTile);

                var wheat = city.CityResources.FirstOrDefault(r => r.ResourceType == ResourceTypes.Wheat);
                if ((wheat?.Amount ?? 0) < cost)
                    throw new InvalidOperationException($"INSUFFICIENT_RESOURCES: Need {cost} Wheat.");

                // Execute
                if (wheat == null)
                {
                    // Should not happen if cost > 0
                    wheat = new CityResource { CityId = city.Id, ResourceType = ResourceTypes.Wheat, Amount = 0 };
                    city.CityResources.Add(wheat);
                    _context.CityResources.Add(wheat);
                }
                wheat.Amount -= cost;
                
                var newCityTile = new CityTile
                {
                    GameId = gameId,
                    CityId = city.Id,
                    TileId = targetTile.Id
                };
                _context.CityTiles.Add(newCityTile);
                
                city.HasActedThisTurn = true;
                
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("City {CityId} expanded to tile {TileId} cost {Cost}", city.Id, targetTile.Id, cost);

                game.TurnInProgress = false;
                await _context.SaveChangesAsync(cancellationToken);
                if (transaction != null) await transaction.CommitAsync(cancellationToken);

                var gameState = await _gameStateService.BuildGameStateAsync(gameId, cancellationToken);
                var response = new ActionStateResponse(gameState);

                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                     var actualKey = $"game:{gameId}:expand:{idempotencyKey}";
                    await _idempotencyStore.TryStoreAsync(actualKey, response, TimeSpan.FromHours(1), cancellationToken);
                }

                return response;
            }
            catch
            {
                game.TurnInProgress = false;
                await _context.SaveChangesAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Expand territory failed");
            throw;
        }
    }
}
