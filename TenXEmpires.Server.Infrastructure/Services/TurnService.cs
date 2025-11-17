using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Domain.Configuration;
using TenXEmpires.Server.Domain.Utilities;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Services;

/// <summary>
/// Service implementation for turn-related business logic and queries.
/// </summary>
public class TurnService : ITurnService
{
    private readonly TenXDbContext _context;
    private readonly ILogger<TurnService> _logger;
    private readonly IGameStateService? _gameStateService;
    private readonly ISaveService? _saveService;
    private readonly IIdempotencyStore? _idempotencyStore;
    private readonly GameSettings? _gameSettings;

    public TurnService(
        TenXDbContext context,
        ILogger<TurnService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Preferred constructor used by DI with additional collaborators
    public TurnService(
        TenXDbContext context,
        IGameStateService gameStateService,
        ISaveService saveService,
        IIdempotencyStore idempotencyStore,
        IOptions<GameSettings> gameSettings,
        ILogger<TurnService> logger)
    {
        _context = context;
        _gameStateService = gameStateService;
        _saveService = saveService;
        _idempotencyStore = idempotencyStore;
        _gameSettings = gameSettings?.Value;
        _logger = logger;
    }

    public async Task<PagedResult<TurnDto>> ListTurnsAsync(
        long gameId,
        ListTurnsQuery query,
        CancellationToken cancellationToken = default)
    {
        // Start with base query filtered by game ID (RLS also applies)
        var baseQuery = _context.Turns
            .AsNoTracking()
            .Where(t => t.GameId == gameId);

        // Apply sorting
        var sortField = (query.Sort ?? TurnSortField.Default).ToLowerInvariant();
        var sortOrder = (query.Order ?? SortOrder.Default).ToLowerInvariant();

        if (!SortOrder.ValidOrders.Contains(sortOrder))
        {
            throw new ArgumentException(
                $"Invalid order '{query.Order}'. Must be one of: {string.Join(", ", SortOrder.ValidOrders)}.",
                nameof(query.Order));
        }

        if (!TurnSortField.ValidFields.Contains(sortField))
        {
            throw new ArgumentException(
                $"Invalid sort field '{query.Sort}'. Must be one of: {string.Join(", ", TurnSortField.ValidFields)}.",
                nameof(query.Sort));
        }

        var isAscending = sortOrder == SortOrder.Ascending;

        baseQuery = sortField switch
        {
            TurnSortField.TurnNo => isAscending
                ? baseQuery.OrderBy(t => t.TurnNo)
                : baseQuery.OrderByDescending(t => t.TurnNo),
            TurnSortField.CommittedAt => isAscending
                ? baseQuery.OrderBy(t => t.CommittedAt)
                : baseQuery.OrderByDescending(t => t.CommittedAt),
            _ => throw new ArgumentException(
                $"Invalid sort field '{query.Sort}'. Must be one of: {string.Join(", ", TurnSortField.ValidFields)}.",
                nameof(query.Sort))
        };

        // Get total count (optional for performance - could be omitted for large datasets)
        var total = await baseQuery.CountAsync(cancellationToken);

        // Apply pagination and fetch turns
        var skip = (query.Page - 1) * query.PageSize;
        var turns = await baseQuery
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // Parse JSON summaries in memory (avoids second database query)
        var items = turns.Select(TurnDto.From).ToList();

        _logger.LogDebug(
            "Listed {Count} turns for game {GameId} (page {Page}, sort: {Sort} {Order})",
            items.Count,
            gameId,
            query.Page,
            sortField,
            sortOrder);

        return new PagedResult<TurnDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total
        };
    }

    public async Task<EndTurnResponse> EndTurnAsync(
        Guid userId,
        long gameId,
        EndTurnCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (_gameStateService is null || _saveService is null || _idempotencyStore is null)
        {
            throw new InvalidOperationException("TurnService not fully configured. Required services are missing.");
        }

        // Idempotency check
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cacheKey = CacheKeys.EndTurnIdempotency(userId, gameId, idempotencyKey);
            var cached = await _idempotencyStore.TryGetAsync<EndTurnResponse>(cacheKey, cancellationToken);
            if (cached != null)
            {
                _logger.LogInformation("Returning cached end-turn response for game {GameId} (key {Key})", gameId, idempotencyKey);
                return cached;
            }
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
            transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        }
        catch (InvalidOperationException) { }
        catch (NotSupportedException) { }

        try
        {
            // Load game + related
            var game = await _context.Games
                .Include(g => g.Map)
                .Include(g => g.ActiveParticipant)
                .FirstOrDefaultAsync(g => g.Id == gameId && g.UserId == userId, cancellationToken);

            if (game is null)
            {
                throw new UnauthorizedAccessException($"Game {gameId} not found or access denied.");
            }

            if (game.ActiveParticipant is null)
            {
                throw new InvalidOperationException("No active participant found for this game.");
            }

            if (!string.Equals(game.ActiveParticipant.Kind, ParticipantKind.Human, StringComparison.OrdinalIgnoreCase) ||
                game.ActiveParticipant.UserId != userId)
            {
                throw new InvalidOperationException("NOT_PLAYER_TURN: It is not your turn.");
            }

            if (game.TurnInProgress)
            {
                throw new InvalidOperationException("TURN_IN_PROGRESS: Another turn operation is in progress.");
            }

            // Guard against concurrent turn operations
            game.TurnInProgress = true;
            await _context.SaveChangesAsync(cancellationToken);

            var turnStart = DateTimeOffset.UtcNow;

            // Autosave snapshot BEFORE processing end-turn
            var preEndState = await _gameStateService.BuildGameStateAsync(gameId, cancellationToken);
            var autosaveId = await _saveService.CreateAutosaveAsync(
                userId,
                gameId,
                game.TurnNo,
                game.ActiveParticipantId!.Value,
                game.MapSchemaVersion,
                game.Map.Code,
                preEndState,
                cancellationToken);

            // End-of-turn systems
            var regenApplied = 0;
            var harvestedTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { ResourceTypes.Wood, 0 },
                { ResourceTypes.Stone, 0 },
                { ResourceTypes.Wheat, 0 },
                { ResourceTypes.Iron, 0 }
            };
            var producedUnitCodes = new List<string>();
            var productionDelayed = 0;

            var allUnits = await _context.Units
                .Include(u => u.Tile)
                .Where(u => u.GameId == gameId)
                .ToListAsync(cancellationToken);

            var occupiedTileIds = allUnits.Select(u => u.TileId).ToHashSet();

            var unitDefs = await _context.UnitDefinitions
                .AsNoTracking()
                .ToDictionaryAsync(ud => ud.Code, ud => ud, cancellationToken);

            var cities = await _context.Cities
                .Include(c => c.Tile)
                .Include(c => c.CityResources)
                .Include(c => c.CityTiles)
                    .ThenInclude(ct => ct.Tile)
                .Where(c => c.GameId == gameId && c.ParticipantId == game.ActiveParticipantId)
                .ToListAsync(cancellationToken);

            foreach (var city in cities)
            {
                // Determine siege state: enemy unit adjacent (hex distance <= 1)
                var cityCube = HexagonalGrid.ConvertOddrToCube(city.Tile.Col, city.Tile.Row);
                var underSiege = allUnits.Any(u => u.ParticipantId != city.ParticipantId &&
                    HexagonalGrid.GetCubeDistance(
                        HexagonalGrid.ConvertOddrToCube(u.Tile.Col, u.Tile.Row),
                        cityCube) <= 1);

                // Regen
                var before = city.Hp;
                var increment = underSiege
                    ? (_gameSettings?.CityRegenUnderSiege ?? 2)
                    : (_gameSettings?.CityRegenNormal ?? 4);
                city.Hp = Math.Min(city.MaxHp, city.Hp + increment);
                if (city.Hp != before) regenApplied++;
                // Harvest from city-owned tiles only and maybe auto-produce
                HarvestCityResources(city, allUnits, harvestedTotals);

                // Auto-produce at most 1 unit/city/turn
                productionDelayed += TryAutoProduceUnit(game, city, unitDefs, occupiedTileIds, producedUnitCodes);
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Commit Turn row with summary (will update aiExecuted after potential AI processing)
            // First, delete any existing Turn for this turn number (defensive check - should not exist, but handles save/load edge cases)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM app.turns WHERE game_id = {gameId} AND turn_no = {game.TurnNo}",
                cancellationToken);

            var duration = (int)Math.Max(0, (DateTimeOffset.UtcNow - turnStart).TotalMilliseconds);
            var summaryObj = new
            {
                regenAppliedCities = regenApplied,
                harvested = harvestedTotals,
                producedUnits = producedUnitCodes,
                productionDelayed,
                aiExecuted = false
            };
            string summaryJson = JsonSerializer.Serialize(summaryObj);
            var turn = new Turn
            {
                GameId = gameId,
                TurnNo = game.TurnNo,
                ParticipantId = game.ActiveParticipantId!.Value,
                CommittedAt = DateTimeOffset.UtcNow,
                DurationMs = duration,
                Summary = summaryJson
            };

            _context.Turns.Add(turn);

            // Autosave already created before end-turn processing

            // Advance to next participant and reset next player's units
            await AdvanceTurnAsync(game, cancellationToken);

            game.LastTurnAt = DateTimeOffset.UtcNow;

            // If next participant(s) are AI, execute them within budget (no autosaves in between)
            var aiExecuted = await TryExecuteAiTurnsAsync(game, TimeSpan.FromMilliseconds(500), cancellationToken);

            if (aiExecuted)
            {
                // Update player's turn summary to reflect AI execution
                var updatedSummaryObj = new
                {
                    regenAppliedCities = regenApplied,
                    harvested = harvestedTotals,
                    producedUnits = producedUnitCodes,
                    productionDelayed,
                    aiExecuted = true
                };
                summaryJson = JsonSerializer.Serialize(updatedSummaryObj);
                turn.Summary = summaryJson;
            }

            // Clear guard
            game.TurnInProgress = false;
            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            // Build final state to return (include turn summary for client convenience)
            var finalState = await _gameStateService.BuildGameStateAsync(gameId, cancellationToken);
            var summaryDoc = JsonDocument.Parse(summaryJson);
            var response = new EndTurnResponse(finalState, summaryDoc, autosaveId);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var cacheKey = CacheKeys.EndTurnIdempotency(userId, gameId, idempotencyKey);
                await _idempotencyStore.TryStoreAsync(cacheKey, response, TimeSpan.FromHours(1), cancellationToken);
            }

            _logger.LogInformation("Ended turn for game {GameId}: turnNo={TurnNo}, regenCities={Regen}", gameId, turn.TurnNo, regenApplied);

            return response;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
    }

    private async Task AdvanceTurnAsync(Game game, CancellationToken cancellationToken)
    {
        // Get non-eliminated participants in stable order
        var participants = await _context.Participants
            .Where(p => p.GameId == game.Id && !p.IsEliminated)
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);

        if (participants.Count == 0)
        {
            throw new InvalidOperationException("No active participants available to advance turn.");
        }

        var currentIndex = participants.FindIndex(p => p.Id == game.ActiveParticipantId);
        if (currentIndex < 0)
        {
            currentIndex = 0; // fallback
        }

        var nextIndex = (currentIndex + 1) % participants.Count;
        var nextParticipant = participants[nextIndex];

        // Increment turn number only after a full round (wrap-around)
        if (nextIndex == 0)
        {
            game.TurnNo += 1;
        }
        game.ActiveParticipantId = nextParticipant.Id;

        // Reset unit action flags for next participant
        var nextUnits = await _context.Units
            .Where(u => u.GameId == game.Id && u.ParticipantId == nextParticipant.Id)
            .ToListAsync(cancellationToken);

        foreach (var unit in nextUnits)
        {
            unit.HasActed = false;
            unit.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> TryExecuteAiTurnsAsync(
        Game game,
        TimeSpan maxDuration,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var executedAny = false;

        while (true)
        {
            // Check current active participant
            var activeParticipant = await _context.Participants
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == game.ActiveParticipantId, cancellationToken);

            if (activeParticipant is null || !string.Equals(activeParticipant.Kind, ParticipantKind.Ai, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var aiStart = DateTimeOffset.UtcNow;

            // Gather AI units and opponent targets
            var aiUnits = await _context.Units
                .Include(u => u.Tile)
                .Include(u => u.Type)
                .Where(u => u.GameId == game.Id && u.ParticipantId == activeParticipant.Id)
                .ToListAsync(cancellationToken);

            var enemyCities = await _context.Cities
                .AsNoTracking()
                .Include(c => c.Tile)
                .Where(c => c.GameId == game.Id && c.ParticipantId != activeParticipant.Id)
                .ToListAsync(cancellationToken);

            var occupied = await _context.Units
                .AsNoTracking()
                .Where(u => u.GameId == game.Id)
                .Select(u => u.TileId)
                .ToListAsync(cancellationToken);
            var occupiedSet = occupied.ToHashSet();

            int unitsMoved = 0;
            int attacks = 0;
            int cityAttacks = 0;

            // Load AI participant cities for harvesting
            var aiCities = await _context.Cities
                .Include(c => c.Tile)
                .Include(c => c.CityResources)
                .Include(c => c.CityTiles)
                    .ThenInclude(ct => ct.Tile)
                .Where(c => c.GameId == game.Id && c.ParticipantId == activeParticipant.Id)
                .ToListAsync(cancellationToken);

            var aiHarvested = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { ResourceTypes.Wood, 0 },
                { ResourceTypes.Stone, 0 },
                { ResourceTypes.Wheat, 0 },
                { ResourceTypes.Iron, 0 }
            };

            // Load enemy units for potential attacks
            var enemyUnits = await _context.Units
                .Include(u => u.Type)
                .Include(u => u.Tile)
                .Where(u => u.GameId == game.Id && u.ParticipantId != activeParticipant.Id)
                .ToListAsync(cancellationToken);
            // Unit definitions for AI production
            var unitDefs = await _context.UnitDefinitions
                .AsNoTracking()
                .ToDictionaryAsync(ud => ud.Code, ud => ud, cancellationToken);
            var producedByAi = new List<string>();
            var productionDelayed = 0;

            foreach (var unit in aiUnits)
            {
                if (unit.HasActed) continue;
                if (!enemyCities.Any()) break;

                var unitCube = HexagonalGrid.ConvertOddrToCube(unit.Tile.Col, unit.Tile.Row);
                // First, attempt an attack if a target is in valid range
                var inRangeEnemy = enemyUnits
                    .Select(t => new { Target = t, Dist = HexagonalGrid.GetCubeDistance(unitCube, HexagonalGrid.ConvertOddrToCube(t.Tile.Col, t.Tile.Row)) })
                    .Where(x => !unit.Type.IsRanged ? x.Dist == 1 : (x.Dist >= unit.Type.RangeMin && x.Dist <= unit.Type.RangeMax))
                    .OrderBy(x => x.Dist)
                    .Select(x => x.Target)
                    .FirstOrDefault();

                if (inRangeEnemy is not null)
                {
                    var outcome = CombatHelper.ResolveAttack(unit, inRangeEnemy);

                    if (outcome.TargetDied)
                    {
                        _context.Units.Remove(inRangeEnemy);
                        occupiedSet.Remove(inRangeEnemy.TileId);
                        enemyUnits.Remove(inRangeEnemy);
                    }
                    if (outcome.AttackerDied)
                    {
                        occupiedSet.Remove(unit.TileId);
                        _context.Units.Remove(unit);
                        // Attacker removed: no movement possible; mark acted by virtue of being resolved
                    }
                    else
                    {
                        unit.HasActed = true;
                        unit.UpdatedAt = DateTimeOffset.UtcNow;
                    }

                    attacks++;
                    continue; // next unit
                }

                // If no in-range unit, attempt city attack if in valid range
                var nearestCityForAttack = enemyCities
                    .OrderBy(c => HexagonalGrid.GetCubeDistance(unitCube, HexagonalGrid.ConvertOddrToCube(c.Tile.Col, c.Tile.Row)))
                    .First();
                var distCity = HexagonalGrid.GetCubeDistance(unitCube, HexagonalGrid.ConvertOddrToCube(nearestCityForAttack.Tile.Col, nearestCityForAttack.Tile.Row));
                var canAttackCity = !unit.Type.IsRanged ? distCity == 1 : (distCity >= unit.Type.RangeMin && distCity <= unit.Type.RangeMax);
                if (canAttackCity)
                {
                    var dmg = CombatHelper.ResolveAttackOnCity(unit, nearestCityForAttack);
                    unit.HasActed = true;
                    unit.UpdatedAt = DateTimeOffset.UtcNow;
                    cityAttacks++;
                    // Capture requires melee end-turn on city tile; handled on movement
                    continue;
                }

                // Choose nearest enemy city by hex distance when not attacking anything
                var nearestCity = enemyCities
                    .OrderBy(c => HexagonalGrid.GetCubeDistance(unitCube, HexagonalGrid.ConvertOddrToCube(c.Tile.Col, c.Tile.Row)))
                    .First();
                var cityCube = HexagonalGrid.ConvertOddrToCube(nearestCity.Tile.Col, nearestCity.Tile.Row);

                // Consider neighbor tiles and pick one that reduces distance and is free
                var currentDist = HexagonalGrid.GetCubeDistance(unitCube, cityCube);
                MapTile? bestTile = null;
                int bestDist = currentDist;

                foreach (var neighborCube in HexagonalGrid.GetHexNeighbours(unitCube))
                {
                    var sq = HexagonalGrid.ConvertCubeToOddr(neighborCube);
                    if (sq.Y < 0 || sq.Y >= game.Map.Height || sq.X < 0 || sq.X >= game.Map.Width) continue;
                    var tile = await _context.MapTiles.FirstOrDefaultAsync(t => t.MapId == game.MapId && t.Row == sq.Y && t.Col == sq.X, cancellationToken);
                    if (tile is null) continue;
                    if (occupiedSet.Contains(tile.Id)) continue; // respect 1UPT
                    if (TerrainTypes.IsWater(tile.Terrain)) continue; // block water and ocean tiles

                    var dist = HexagonalGrid.GetCubeDistance(neighborCube, cityCube);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTile = tile;
                    }
                }

                if (bestTile is not null)
                {
                    // Move unit one tile closer
                    occupiedSet.Remove(unit.TileId);
                    unit.TileId = bestTile.Id;
                    unit.HasActed = true;
                    unit.UpdatedAt = DateTimeOffset.UtcNow;
                    occupiedSet.Add(bestTile.Id);
                    unitsMoved++;

                    // If moved onto a defeated enemy city tile and unit is melee, capture
                    var cityHere = await _context.Cities
                        .Include(c => c.Tile)
                        .FirstOrDefaultAsync(c => c.GameId == game.Id && c.TileId == bestTile.Id, cancellationToken);
                    if (cityHere != null && cityHere.ParticipantId != unit.ParticipantId && cityHere.Hp <= 0 && !unit.Type.IsRanged)
                    {
                        await CityCaptureHelper.CaptureCityAsync(_context, cityHere, unit.ParticipantId, cancellationToken);
                        enemyCities.RemoveAll(c => c.Id == cityHere.Id);
                    }
                }
            }

            // After unit actions, harvest AI-owned city tiles and try auto production
            var allUnits = new List<Unit>();
            allUnits.AddRange(aiUnits);
            allUnits.AddRange(enemyUnits);
            foreach (var city in aiCities)
            {
                HarvestCityResources(city, allUnits, aiHarvested);
                productionDelayed += TryAutoProduceUnit(game, city, unitDefs, occupiedSet, producedByAi);
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Note: We don't create a separate Turn record for AI turns because the database
            // constraint ux_turns only allows one Turn per (game_id, turn_no). The human player's
            // Turn record already includes an aiExecuted flag in its summary to indicate AI actions occurred.

            // Advance to next participant (could increment TurnNo on wrap)
            await AdvanceTurnAsync(game, cancellationToken);

            executedAny = true;

            if ((DateTimeOffset.UtcNow - started) >= maxDuration)
            {
                break;
            }

            // If next participant is not AI, stop
            var nextIsAi = await _context.Participants
                .AsNoTracking()
                .AnyAsync(p => p.Id == game.ActiveParticipantId && p.Kind == ParticipantKind.Ai, cancellationToken);
            if (!nextIsAi)
            {
                break;
            }
        }

        return executedAny;
    }

    private void HarvestCityResources(
        City city,
        List<Unit> allUnits,
        Dictionary<string, int> harvestedTotals)
    {
        foreach (var link in city.CityTiles)
        {
            var tile = link.Tile;
            if (tile.ResourceType is null || tile.ResourceAmount <= 0)
                continue;

            var blockingUnit = allUnits.FirstOrDefault(u => u.ParticipantId != city.ParticipantId && u.TileId == tile.Id);
            if (blockingUnit is not null)
            {
                _logger.LogDebug(
                    "City {CityId} skipped harvesting tile {TileId} due to enemy unit {UnitId} occupying it",
                    city.Id,
                    tile.Id,
                    blockingUnit.Id);
                continue;
            }

            var cr = city.CityResources.FirstOrDefault(r => r.ResourceType == tile.ResourceType);
            if (cr is null)
            {
                cr = new CityResource { CityId = city.Id, ResourceType = tile.ResourceType, Amount = 0 };
                city.CityResources.Add(cr);
                _context.CityResources.Add(cr);
            }

            cr.Amount += 1; // produce 1 per tile
            tile.ResourceAmount -= 1; // consume 1 from tile stock

            if (harvestedTotals.ContainsKey(tile.ResourceType))
            {
                harvestedTotals[tile.ResourceType] += 1;
            }
            else
            {
                harvestedTotals[tile.ResourceType] = 1;
            }
        }
    }

    private int TryAutoProduceUnit(
        Game game,
        City city,
        Dictionary<string, UnitDefinition> unitDefs,
        HashSet<long> occupiedTileIds,
        List<string> producedUnitCodes)
    {
        string? produceCode = null;
        int costAmount = 0;
        string? costResource = null;

        var iron = city.CityResources.FirstOrDefault(r => r.ResourceType == ResourceTypes.Iron)?.Amount ?? 0;
        var stone = city.CityResources.FirstOrDefault(r => r.ResourceType == ResourceTypes.Stone)?.Amount ?? 0;

        if (iron >= 10 && unitDefs.TryGetValue(UnitTypes.Warrior, out _))
        {
            produceCode = UnitTypes.Warrior;
            costResource = ResourceTypes.Iron;
            costAmount = 10;
        }
        else if (stone >= 10 && unitDefs.TryGetValue(UnitTypes.Slinger, out _))
        {
            produceCode = UnitTypes.Slinger;
            costResource = ResourceTypes.Stone;
            costAmount = 10;
        }

        if (produceCode is null || !unitDefs.TryGetValue(produceCode, out var def))
        {
            return 0;
        }

        MapTile? spawnTile = null;
        if (!occupiedTileIds.Contains(city.TileId))
        {
            spawnTile = city.Tile;
        }
        else
        {
            var random = new Random(HashCode.Combine(game.RngSeed, game.TurnNo, city.Id));
            var excluded = occupiedTileIds.ToArray();

            // Build neighbor tiles list and use HexagonalGrid.FindAdjacentTile
            var centerCube = HexagonalGrid.ConvertOddrToCube(city.Tile.Col, city.Tile.Row);
            var neighborCubes = HexagonalGrid.GetHexNeighbours(centerCube);
            var neighborTiles = new List<MapTile>();
            foreach (var cube in neighborCubes)
            {
                var sq = HexagonalGrid.ConvertCubeToOddr(cube);
                if (sq.Y < 0 || sq.Y >= game.Map.Height || sq.X < 0 || sq.X >= game.Map.Width) continue;
                var tile = _context.MapTiles.FirstOrDefault(t => t.MapId == game.MapId && t.Row == sq.Y && t.Col == sq.X);
                if (tile is not null)
                {
                    neighborTiles.Add(tile);
                }
            }

            spawnTile = HexagonalGrid.FindAdjacentTile(city.Tile, neighborTiles, game.Map.Width, game.Map.Height, random, excluded);
        }

        if (spawnTile is not null)
        {
            var unit = new Unit
            {
                GameId = game.Id,
                ParticipantId = city.ParticipantId,
                TypeId = def.Id,
                TileId = spawnTile.Id,
                Hp = def.Health,
                HasActed = false
            };
            _context.Units.Add(unit);
            occupiedTileIds.Add(spawnTile.Id);

            var costCr = city.CityResources.First(r => r.ResourceType == costResource);
            costCr.Amount -= costAmount;
            producedUnitCodes.Add(produceCode);
            return 0;
        }

        return 1;
    }

    // Using HexagonalGrid.FindAdjacentTile above; removed old custom neighbor finder.
}
