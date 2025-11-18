using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Utilities;

namespace TenXEmpires.Server.Infrastructure.Services;

/// <summary>
/// Service for creating and managing game saves.
/// </summary>
public class SaveService : ISaveService
{
    private const int AutosaveCapacity = 5;

    private readonly TenXDbContext _context;
    private readonly ILogger<SaveService> _logger;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IGameStateService _gameStateService;

    public SaveService(
        TenXDbContext context,
        ILogger<SaveService> logger,
        IIdempotencyStore idempotencyStore,
        IGameStateService gameStateService)
    {
        _context = context;
        _logger = logger;
        _idempotencyStore = idempotencyStore;
        _gameStateService = gameStateService;
    }

    public async Task<long> CreateAutosaveAsync(
        Guid userId,
        long gameId,
        int turnNo,
        long activeParticipantId,
        int schemaVersion,
        string mapCode,
        GameStateDto state,
        CancellationToken cancellationToken = default)
    {
        // Serialize state to JSON (store as-is; compression handled by storage engine if configured)
        var stateJson = JsonSerializer.Serialize(state);

        var save = new Save
        {
            UserId = userId,
            GameId = gameId,
            Kind = "autosave",
            Name = $"Autosave - Turn {turnNo}",
            Slot = null,
            TurnNo = turnNo,
            ActiveParticipantId = activeParticipantId,
            SchemaVersion = schemaVersion,
            MapCode = mapCode,
            State = stateJson,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.Saves.Add(save);
        await _context.SaveChangesAsync(cancellationToken);

        // Enforce ring buffer: keep most recent AutosaveCapacity autosaves
        var autosaves = await _context.Saves
            .Where(s => s.GameId == gameId && s.Kind == "autosave")
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.Id, s.CreatedAt })
            .ToListAsync(cancellationToken);

        if (autosaves.Count > AutosaveCapacity)
        {
            var toDeleteIds = autosaves
                .Skip(AutosaveCapacity)
                .Select(s => s.Id)
                .ToArray();

            int deleted;
            try
            {
                deleted = await _context.Saves
                    .Where(s => toDeleteIds.Contains(s.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }
            catch (NotSupportedException)
            {
                var stale = await _context.Saves.Where(s => toDeleteIds.Contains(s.Id)).ToListAsync(cancellationToken);
                _context.Saves.RemoveRange(stale);
                await _context.SaveChangesAsync(cancellationToken);
                deleted = stale.Count;
            }
            catch (InvalidOperationException)
            {
                var stale = await _context.Saves.Where(s => toDeleteIds.Contains(s.Id)).ToListAsync(cancellationToken);
                _context.Saves.RemoveRange(stale);
                await _context.SaveChangesAsync(cancellationToken);
                deleted = stale.Count;
            }

            _logger.LogInformation(
                "Autosave ring buffer enforced for game {GameId}: kept {Kept}, deleted {Deleted}",
                gameId,
                AutosaveCapacity,
                deleted);
        }

        return save.Id;
    }

    public async Task<GameSavesListDto> ListSavesAsync(long gameId, CancellationToken cancellationToken = default)
    {
        // Query all saves for the game
        var saves = await _context.Saves
            .AsNoTracking()
            .Where(s => s.GameId == gameId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        // Group by kind and project to DTOs
        var manualSaves = saves
            .Where(s => s.Kind == "manual")
            .Select(SaveManualDto.From)
            .ToList();

        var autosaves = saves
            .Where(s => s.Kind == "autosave")
            .Select(SaveAutosaveDto.From)
            .ToList();

        _logger.LogDebug("Listed {ManualCount} manual saves and {AutosaveCount} autosaves for game {GameId}",
            manualSaves.Count, autosaves.Count, gameId);

        return new GameSavesListDto(manualSaves, autosaves);
    }

    public async Task<SaveCreatedDto> CreateManualAsync(
        Guid userId,
        long gameId,
        CreateManualSaveCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            throw new ArgumentException("Command cannot be null.");
        }

        if (command.Slot < 1 || command.Slot > 3)
        {
            throw new ArgumentException("Invalid slot. Slot must be between 1 and 3.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ArgumentException("Invalid name. Name cannot be empty.");
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cacheKey = CacheKeys.CreateManualSaveIdempotency(userId, gameId, idempotencyKey);
            var cached = await _idempotencyStore.TryGetAsync<SaveCreatedDto>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return cached;
            }
        }

        // Verify game belongs to user and load required info
        var game = await _context.Games
            .Include(g => g.Map)
            .AsNoTracking()
            .SingleOrDefaultAsync(g => g.Id == gameId && g.UserId == userId, cancellationToken);

        if (game is null)
        {
            throw new UnauthorizedAccessException($"Game {gameId} not found or access denied.");
        }

        if (game.ActiveParticipantId is null)
        {
            throw new InvalidOperationException("No active participant found for this game.");
        }

        // Build snapshot of current game state
        var state = await _gameStateService.BuildGameStateAsync(gameId, cancellationToken);
        var stateJson = JsonSerializer.Serialize(state);

        // Transactional upsert by (userId, gameId, slot) for kind='manual'
        using var trx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var existing = await _context.Saves
                .Where(s => s.UserId == userId && s.GameId == gameId && s.Kind == "manual" && s.Slot == command.Slot)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is null)
            {
                var save = new Save
                {
                    UserId = userId,
                    GameId = gameId,
                    Kind = "manual",
                    Name = command.Name.Trim(),
                    Slot = command.Slot,
                    TurnNo = game.TurnNo,
                    ActiveParticipantId = game.ActiveParticipantId.Value,
                    SchemaVersion = game.MapSchemaVersion,
                    MapCode = game.Map.Code,
                    State = stateJson,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                _context.Saves.Add(save);
                await _context.SaveChangesAsync(cancellationToken);

                await trx.CommitAsync(cancellationToken);

                var created = SaveCreatedDto.From(save);

                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var cacheKey = CacheKeys.CreateManualSaveIdempotency(userId, gameId, idempotencyKey);
                    await _idempotencyStore.TryStoreAsync(cacheKey, created, TimeSpan.FromHours(24), cancellationToken);
                }

                _logger.LogInformation("Created manual save slot {Slot} for game {GameId} by user {UserId}", command.Slot, gameId, userId);
                return created;
            }
            else
            {
                existing.Name = command.Name.Trim();
                existing.TurnNo = game.TurnNo;
                existing.ActiveParticipantId = game.ActiveParticipantId.Value;
                existing.SchemaVersion = game.MapSchemaVersion;
                existing.MapCode = game.Map.Code;
                existing.State = stateJson;
                existing.CreatedAt = DateTimeOffset.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);
                await trx.CommitAsync(cancellationToken);

                var created = SaveCreatedDto.From(existing);

                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var cacheKey = CacheKeys.CreateManualSaveIdempotency(userId, gameId, idempotencyKey);
                    await _idempotencyStore.TryStoreAsync(cacheKey, created, TimeSpan.FromHours(24), cancellationToken);
                }

                _logger.LogInformation("Overwrote manual save slot {Slot} for game {GameId} by user {UserId}", command.Slot, gameId, userId);
                return created;
            }
        }
        catch (DbUpdateException ex)
        {
            await trx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create manual save for game {GameId} by user {UserId}", gameId, userId);
            throw new InvalidOperationException("SAVE_CONFLICT: Could not upsert manual save due to a conflict.");
        }
        catch
        {
            await trx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> DeleteManualAsync(
        long gameId,
        int slot,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (slot < 1 || slot > 3)
        {
            throw new ArgumentException("Invalid slot. Slot must be between 1 and 3.");
        }

        // Check idempotency if key provided
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cachedKey = CacheKeys.DeleteManualSaveIdempotency(gameId, slot, idempotencyKey);
            var cachedResponse = await _idempotencyStore.TryGetAsync<string>(cachedKey, cancellationToken);
            if (cachedResponse is not null)
            {
                _logger.LogInformation(
                    "Returning cached manual save delete response for idempotency key {IdempotencyKey} (game {GameId}, slot {Slot})",
                    idempotencyKey,
                    gameId,
                    slot);
                return cachedResponse == "deleted";
            }
        }

        // Attempt to delete the manual save
        // Try set-based delete when available, else fall back to tracked deletion
        try
        {
            var deletedCount = await _context.Saves
                .Where(s => s.GameId == gameId && s.Kind == "manual" && s.Slot == slot)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount == 0)
            {
                _logger.LogWarning("Manual save not found for game {GameId} slot {Slot}", gameId, slot);
                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var cachedKey = CacheKeys.DeleteManualSaveIdempotency(gameId, slot, idempotencyKey!);
                    await _idempotencyStore.TryStoreAsync(cachedKey, "not_found", TimeSpan.FromHours(24), cancellationToken);
                }
                return false;
            }
        }
        catch (NotSupportedException)
        {
            var entity = await _context.Saves
                .Where(s => s.GameId == gameId && s.Kind == "manual" && s.Slot == slot)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity is null)
            {
                _logger.LogWarning("Manual save not found for game {GameId} slot {Slot}", gameId, slot);
                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var cachedKey = CacheKeys.DeleteManualSaveIdempotency(gameId, slot, idempotencyKey!);
                    await _idempotencyStore.TryStoreAsync(cachedKey, "not_found", TimeSpan.FromHours(24), cancellationToken);
                }
                return false;
            }

            _context.Saves.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            var entity = await _context.Saves
                .Where(s => s.GameId == gameId && s.Kind == "manual" && s.Slot == slot)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity is null)
            {
                _logger.LogWarning("Manual save not found for game {GameId} slot {Slot}", gameId, slot);
                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var cachedKey = CacheKeys.DeleteManualSaveIdempotency(gameId, slot, idempotencyKey!);
                    await _idempotencyStore.TryStoreAsync(cachedKey, "not_found", TimeSpan.FromHours(24), cancellationToken);
                }
                return false;
            }

            _context.Saves.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Deleted manual save slot {Slot} for game {GameId}", slot, gameId);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cachedKey = CacheKeys.DeleteManualSaveIdempotency(gameId, slot, idempotencyKey);
            await _idempotencyStore.TryStoreAsync(cachedKey, "deleted", TimeSpan.FromHours(24), cancellationToken);
        }

        return true;
    }

    public async Task<LoadSaveResponse> LoadAsync(
        Guid userId,
        long saveId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        // Check idempotency if key provided
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cacheKey = CacheKeys.LoadSaveIdempotency(userId, saveId, idempotencyKey);
            var cached = await _idempotencyStore.TryGetAsync<LoadSaveResponse>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                _logger.LogInformation(
                    "Returning cached load save response for idempotency key {IdempotencyKey} (save {SaveId})",
                    idempotencyKey,
                    saveId);
                return cached;
            }
        }

        // Load save with RLS; verify ownership
        var save = await _context.Saves
            .Include(s => s.Game)
                .ThenInclude(g => g.Map)
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == saveId, cancellationToken);

        if (save is null)
        {
            throw new KeyNotFoundException($"Save {saveId} not found.");
        }

        if (save.UserId != userId)
        {
            throw new UnauthorizedAccessException($"Save {saveId} does not belong to user {userId}.");
        }

        // Validate schema version compatibility
        var currentSchemaVersion = save.Game.MapSchemaVersion;
        if (save.SchemaVersion != currentSchemaVersion)
        {
            _logger.LogWarning(
                "Schema version mismatch for save {SaveId}: save has {SaveSchema}, game has {GameSchema}",
                saveId,
                save.SchemaVersion,
                currentSchemaVersion);
            throw new InvalidOperationException("SCHEMA_MISMATCH: Save schema version is incompatible with current game schema.");
        }

        // Deserialize saved state
        GameStateDto savedState;
        try
        {
            savedState = JsonSerializer.Deserialize<GameStateDto>(save.State)
                ?? throw new InvalidOperationException("Failed to deserialize save state: result was null.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize save state for save {SaveId}", saveId);
            throw new InvalidOperationException("Failed to deserialize save state.", ex);
        }

        // Begin transaction to replace game state
        using var trx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var gameId = save.GameId;

            // Load the game for update
            var game = await _context.Games
                .Include(g => g.Map)
                .SingleOrDefaultAsync(g => g.Id == gameId, cancellationToken);

            if (game is null)
            {
                throw new InvalidOperationException($"Game {gameId} not found.");
            }

            // Delete existing game state entities
            // Use raw SQL for reliable deletion that works with RLS
            // Delete in order to respect FK constraints
            
            // 1. City resources (references cities)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"DELETE FROM app.city_resources 
                   WHERE city_id IN (SELECT id FROM app.cities WHERE game_id = {gameId})",
                cancellationToken);

            // 2. City tiles (has game_id column)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM app.city_tiles WHERE game_id = {gameId}",
                cancellationToken);

            // 3. Cities (references game)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM app.cities WHERE game_id = {gameId}",
                cancellationToken);

            // 4. Units (references game)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM app.units WHERE game_id = {gameId}",
                cancellationToken);

            // 5. Per-game tile states
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM app.game_tile_states WHERE game_id = {gameId}",
                cancellationToken);

            // 6. Turns (delete turns at or after the restored turn number to avoid duplicate key violations)
            // We restore to save.TurnNo, so delete turns >= save.TurnNo since we'll be recreating them
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM app.turns WHERE game_id = {gameId} AND turn_no >= {save.TurnNo}",
                cancellationToken);

            // Update game metadata
            game.TurnNo = save.TurnNo;
            game.ActiveParticipantId = save.ActiveParticipantId;
            // Always set turnInProgress to false when loading a save to ensure clean state
            // (autosaves are created before ending turn, so they may have turnInProgress=true)
            game.TurnInProgress = false;
            game.Status = savedState.Game.Status;
            game.LastTurnAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Restore entities from saved state
            // Note: We need to map from DTO back to entity and get tile IDs
            var tileMap = await _context.MapTiles
                .Where(t => t.MapId == game.MapId)
                .ToDictionaryAsync(t => new { t.Row, t.Col }, t => t.Id, cancellationToken);

            // Get unit type mapping (code -> ID)
            var unitTypeMap = await _context.UnitDefinitions
                .ToDictionaryAsync(ud => ud.Code, ud => ud.Id, cancellationToken);

            // Restore units
            foreach (var unitDto in savedState.Units)
            {
                var tileId = tileMap[new { unitDto.Row, unitDto.Col }];
                var typeId = unitTypeMap[unitDto.TypeCode];
                var unit = new Unit
                {
                    ParticipantId = unitDto.ParticipantId,
                    GameId = gameId,
                    TypeId = typeId,
                    Hp = unitDto.Hp,
                    HasActed = unitDto.HasActed,
                    TileId = tileId
                };
                _context.Units.Add(unit);
            }

            // Restore cities
            var cityIdMapping = new Dictionary<long, long>(); // old ID -> new ID
            foreach (var cityDto in savedState.Cities)
            {
                var tileId = tileMap[new { cityDto.Row, cityDto.Col }];
                var city = new City
                {
                    ParticipantId = cityDto.ParticipantId,
                    GameId = gameId,
                    Hp = cityDto.Hp,
                    MaxHp = cityDto.MaxHp,
                    TileId = tileId
                };
                _context.Cities.Add(city);
                await _context.SaveChangesAsync(cancellationToken); // Save to get ID
                cityIdMapping[cityDto.Id] = city.Id;
            }

            // Restore city tiles
            foreach (var ctDto in savedState.CityTiles)
            {
                var newCityId = cityIdMapping[ctDto.CityId];
                var cityTile = new CityTile
                {
                    GameId = gameId,
                    CityId = newCityId,
                    TileId = ctDto.TileId
                };
                _context.CityTiles.Add(cityTile);
            }

            // Restore city resources
            foreach (var crDto in savedState.CityResources)
            {
                var newCityId = cityIdMapping[crDto.CityId];
                var cityResource = new CityResource
                {
                    CityId = newCityId,
                    ResourceType = crDto.ResourceType,
                    Amount = crDto.Amount
                };
                _context.CityResources.Add(cityResource);
            }

            // Restore per-game tile resource state
            if (savedState.GameTiles is { Count: > 0 })
            {
                foreach (var tileDto in savedState.GameTiles)
                {
                    _context.GameTileStates.Add(new GameTileState
                    {
                        GameId = gameId,
                        TileId = tileDto.TileId,
                        ResourceAmount = tileDto.ResourceAmount
                    });
                }
            }
            else
            {
                // Fallback for legacy saves without per-tile data: copy from template map
                var mapTilesForGame = await _context.MapTiles
                    .Where(t => t.MapId == game.MapId)
                    .ToListAsync(cancellationToken);

                foreach (var tile in mapTilesForGame)
                {
                    _context.GameTileStates.Add(new GameTileState
                    {
                        GameId = gameId,
                        TileId = tile.Id,
                        ResourceAmount = tile.ResourceAmount
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await trx.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Loaded save {SaveId} into game {GameId} for user {UserId} at turn {TurnNo}",
                saveId,
                gameId,
                userId,
                save.TurnNo);

            // Rebuild game state
            var newState = await _gameStateService.BuildGameStateAsync(gameId, cancellationToken);
            var response = new LoadSaveResponse(gameId, newState);

            // Cache response if idempotency key provided
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var cacheKey = CacheKeys.LoadSaveIdempotency(userId, saveId, idempotencyKey);
                await _idempotencyStore.TryStoreAsync(cacheKey, response, TimeSpan.FromHours(24), cancellationToken);
            }

            return response;
        }
        catch
        {
            await trx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
