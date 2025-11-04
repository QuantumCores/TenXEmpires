using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenXEmpires.Server.Domain.Configuration;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Domain.Utilities;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Services;

/// <summary>
/// Service implementation for game-related business logic and queries.
/// </summary>
public class GameService : IGameService
{
    private readonly TenXDbContext _context;
    private readonly ILogger<GameService> _logger;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IAiNameGenerator _aiNameGenerator;
    private readonly IGameSeedingService _gameSeedingService;
    private readonly IGameStateService _gameStateService;
    private readonly GameSettings _gameSettings;

    public GameService(
        TenXDbContext context,
        ILogger<GameService> logger,
        IIdempotencyStore idempotencyStore,
        IAiNameGenerator aiNameGenerator,
        IGameSeedingService gameSeedingService,
        IGameStateService gameStateService,
        IOptions<GameSettings> gameSettings)
    {
        _context = context;
        _logger = logger;
        _idempotencyStore = idempotencyStore;
        _aiNameGenerator = aiNameGenerator;
        _gameSeedingService = gameSeedingService;
        _gameStateService = gameStateService;
        _gameSettings = gameSettings.Value;
    }

    public async Task<PagedResult<GameListItemDto>> ListGamesAsync(
        Guid userId,
        ListGamesQuery query,
        CancellationToken cancellationToken = default)
    {
        // Start with base query filtered by user (defense-in-depth with RLS)
        var baseQuery = _context.Games
            .AsNoTracking()
            .Where(g => g.UserId == userId);

        // Apply status filter if provided
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var normalizedStatus = query.Status.ToLowerInvariant();
            if (GameStatus.ValidStatuses.Contains(normalizedStatus))
            {
                baseQuery = baseQuery.Where(g => g.Status == normalizedStatus);
            }
            else
            {
                throw new ArgumentException(
                    $"Invalid status '{query.Status}'. Must be one of: {string.Join(", ", GameStatus.ValidStatuses)}.",
                    nameof(query.Status));
            }
        }

        // Apply sorting
        var sortField = (query.Sort ?? GameSortField.Default).ToLowerInvariant();
        var sortOrder = (query.Order ?? SortOrder.Default).ToLowerInvariant();

        if (!SortOrder.ValidOrders.Contains(sortOrder))
        {
            throw new ArgumentException(
                $"Invalid order '{query.Order}'. Must be one of: {string.Join(", ", SortOrder.ValidOrders)}.",
                nameof(query.Order));
        }

        if (!GameSortField.ValidFields.Contains(sortField))
        {
            throw new ArgumentException(
                $"Invalid sort field '{query.Sort}'. Must be one of: {string.Join(", ", GameSortField.ValidFields)}.",
                nameof(query.Sort));
        }

        var isAscending = sortOrder == SortOrder.Ascending;

        baseQuery = sortField switch
        {
            GameSortField.StartedAt => isAscending
                ? baseQuery.OrderBy(g => g.StartedAt)
                : baseQuery.OrderByDescending(g => g.StartedAt),
            GameSortField.LastTurnAt => isAscending
                ? baseQuery.OrderBy(g => g.LastTurnAt ?? g.StartedAt)
                : baseQuery.OrderByDescending(g => g.LastTurnAt ?? g.StartedAt),
            GameSortField.TurnNo => isAscending
                ? baseQuery.OrderBy(g => g.TurnNo)
                : baseQuery.OrderByDescending(g => g.TurnNo),
            _ => throw new ArgumentException(
                $"Invalid sort field '{query.Sort}'. Must be one of: {string.Join(", ", GameSortField.ValidFields)}.",
                nameof(query.Sort))
        };

        _logger.LogInformation("ARD: baseQuery {0}", baseQuery.ToQueryString());

        // Get total count (optional for performance - could be deferred)
        var total = await baseQuery.CountAsync(cancellationToken);

        // Apply pagination
        var skip = (query.Page - 1) * query.PageSize;
        var items = await baseQuery
            .Skip(skip)
            .Take(query.PageSize)
            .Select(g => new GameListItemDto(
                g.Id,
                g.Status,
                g.TurnNo,
                g.MapId,
                g.MapSchemaVersion,
                g.StartedAt,
                g.FinishedAt,
                g.LastTurnAt))
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Listed {Count} games for user {UserId} (page {Page}, status: {Status}, sort: {Sort} {Order})",
            items.Count,
            userId,
            query.Page,
            query.Status ?? "all",
            sortField,
            sortOrder);

        return new PagedResult<GameListItemDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total
        };
    }

    public async Task<GameCreatedResponse> CreateGameAsync(
        Guid userId,
        CreateGameCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        // Check idempotency if key provided
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cachedKey = CacheKeys.CreateGameIdempotency(userId, idempotencyKey);
            var cachedResponse = await _idempotencyStore.TryGetAsync<GameCreatedResponse>(
                cachedKey,
                cancellationToken);

            if (cachedResponse != null)
            {
                _logger.LogInformation(
                    "Returning cached response for idempotency key {IdempotencyKey} (game {GameId})",
                    idempotencyKey,
                    cachedResponse.Id);
                return cachedResponse;
            }
        }

        // Validate user game limit
        var activeGameCount = await _context.Games
            .Where(g => g.UserId == userId && g.Status == GameStatus.Active)
            .CountAsync(cancellationToken);

        if (activeGameCount >= _gameSettings.MaxActiveGamesPerUser)
        {
            throw new InvalidOperationException(
                $"Game limit reached. You can have at most {_gameSettings.MaxActiveGamesPerUser} active games.");
        }

        // Resolve map
        var mapCode = command.MapCode ?? _gameSettings.DefaultMapCode;
        var map = await _context.Maps
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Code == mapCode, cancellationToken);

        if (map == null)
        {
            throw new InvalidOperationException($"Map with code '{mapCode}' not found.");
        }

        if (map.SchemaVersion != _gameSettings.AcceptedMapSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Map schema version mismatch. Expected version {_gameSettings.AcceptedMapSchemaVersion}, but map has version {map.SchemaVersion}.");
        }

        // Parse settings JSON
        var settingsJson = command.Settings != null
            ? JsonSerializer.Serialize(command.Settings)
            : "{}";

        // Generate RNG seed for deterministic game behavior
        var rngSeed = GenerateRngSeed();

        // Begin transaction for game creation
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Create game entity
            var game = new Game
            {
                UserId = userId,
                MapId = map.Id,
                MapSchemaVersion = map.SchemaVersion,
                TurnNo = 1,
                TurnInProgress = false,
                RngSeed = rngSeed,
                RngVersion = "v1",
                Status = GameStatus.Active,
                StartedAt = DateTimeOffset.UtcNow,
                Settings = settingsJson
            };

            _context.Games.Add(game);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created game {GameId} for user {UserId} on map {MapCode}",
                game.Id,
                userId,
                mapCode);

            // Create participants (human + AI)
            var humanDisplayName = string.IsNullOrWhiteSpace(command.DisplayName)
                ? "Player"
                : command.DisplayName.Trim();

            var humanParticipant = new Participant
            {
                GameId = game.Id,
                Kind = ParticipantKind.Human,
                UserId = userId,
                DisplayName = humanDisplayName,
                IsEliminated = false
            };

            // Generate fun AI opponent name
            var aiDisplayName = _aiNameGenerator.GenerateName(rngSeed);

            var aiParticipant = new Participant
            {
                GameId = game.Id,
                Kind = ParticipantKind.Ai,
                UserId = null,
                DisplayName = aiDisplayName,
                IsEliminated = false
            };

            _context.Participants.AddRange(humanParticipant, aiParticipant);
            await _context.SaveChangesAsync(cancellationToken);

            // Set active participant to human
            game.ActiveParticipantId = humanParticipant.Id;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created participants for game {GameId}: human={HumanId}, ai={AiId}",
                game.Id,
                humanParticipant.Id,
                aiParticipant.Id);

            // Seed cities, units, and resources
            await _gameSeedingService.SeedGameEntitiesAsync(
                game.Id,
                map.Id,
                humanParticipant.Id,
                aiParticipant.Id,
                rngSeed,
                cancellationToken);

            // Commit transaction
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Game {GameId} created successfully", game.Id);

            // Build complete game state
            var gameState = await _gameStateService.BuildGameStateAsync(game.Id, cancellationToken);

            var response = new GameCreatedResponse(game.Id, gameState);

            // Store in idempotency cache if key provided
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var cachedKey = CacheKeys.CreateGameIdempotency(userId, idempotencyKey);
                await _idempotencyStore.TryStoreAsync(
                    cachedKey,
                    response,
                    TimeSpan.FromHours(24),
                    cancellationToken);
            }

            return response;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create game for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> VerifyGameAccessAsync(
        Guid userId,
        long gameId,
        CancellationToken cancellationToken = default)
    {
        // Check if game exists and belongs to user (defense-in-depth with RLS)
        var gameExists = await _context.Games
            .AsNoTracking()
            .AnyAsync(g => g.Id == gameId && g.UserId == userId, cancellationToken);

        return gameExists;
    }

    public async Task<GameDetailDto?> GetGameDetailAsync(
        Guid userId,
        long gameId,
        CancellationToken cancellationToken = default)
    {
        // Defense-in-depth: filter by both Id and UserId even with RLS enabled
        var game = await _context.Games
            .AsNoTracking()
            .SingleOrDefaultAsync(g => g.Id == gameId && g.UserId == userId, cancellationToken);

        if (game is null)
        {
            return null;
        }

        // Map entity to DTO (includes safe JSON parsing for settings)
        return GameDetailDto.From(game);
    }

    public async Task<bool> DeleteGameAsync(
        Guid userId,
        long gameId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        // Check idempotency if key provided
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cachedKey = CacheKeys.DeleteGameIdempotency(userId, idempotencyKey);
            var cachedResponse = await _idempotencyStore.TryGetAsync<string>(
                cachedKey,
                cancellationToken);

            if (cachedResponse != null)
            {
                _logger.LogInformation(
                    "Returning cached delete response for idempotency key {IdempotencyKey} (game {GameId})",
                    idempotencyKey,
                    gameId);
                // Return true if marked as "deleted", false if marked as "not_found"
                return cachedResponse == "deleted";
            }
        }

        // Begin transaction for atomic deletion
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Load game stub to verify ownership via RLS
            // Defense-in-depth: filter by both Id and UserId even with RLS enabled
            var game = await _context.Games
                .Where(g => g.Id == gameId && g.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (game == null)
            {
                // Game not found or not accessible - return false
                _logger.LogWarning(
                    "Game {GameId} not found or user {UserId} doesn't have access for deletion",
                    gameId,
                    userId);

                // Store not-found result in idempotency cache if key provided
                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var cachedKey = CacheKeys.DeleteGameIdempotency(userId, idempotencyKey);
                    await _idempotencyStore.TryStoreAsync(
                        cachedKey,
                        "not_found",
                        TimeSpan.FromHours(24),
                        cancellationToken);
                }

                return false;
            }

            // Delete child entities in proper order to respect FK constraints
            // Note: This is manual cascade delete. In production, FK constraints should have ON DELETE CASCADE.

            // 1. Delete city resources (references cities)
            var cityResourcesDeleted = await _context.Database
                .ExecuteSqlInterpolatedAsync(
                    $@"DELETE FROM app.city_resources 
                       WHERE city_id IN (SELECT id FROM app.cities WHERE game_id = {gameId})",
                    cancellationToken);

            // 2. Delete city tiles (references cities)
            var cityTilesDeleted = await _context.Database
                .ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM app.city_tiles WHERE game_id = {gameId}",
                    cancellationToken);

            // 3. Delete cities (references game)
            var citiesDeleted = await _context.Database
                .ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM app.cities WHERE game_id = {gameId}",
                    cancellationToken);

            // 4. Delete units (references game)
            var unitsDeleted = await _context.Database
                .ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM app.units WHERE game_id = {gameId}",
                    cancellationToken);

            // 5. Delete turns (references game and participants)
            var turnsDeleted = await _context.Database
                .ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM app.turns WHERE game_id = {gameId}",
                    cancellationToken);

            // 6. Delete saves (references game)
            var savesDeleted = await _context.Database
                .ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM app.saves WHERE game_id = {gameId}",
                    cancellationToken);

            // 7. Delete participants (references game)
            var participantsDeleted = await _context.Database
                .ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM app.participants WHERE game_id = {gameId}",
                    cancellationToken);

            // 8. Finally, delete the game itself
            _context.Games.Remove(game);
            await _context.SaveChangesAsync(cancellationToken);

            // Commit transaction
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully deleted game {GameId} for user {UserId} " +
                "(cityResources: {CityResources}, cityTiles: {CityTiles}, cities: {Cities}, " +
                "units: {Units}, turns: {Turns}, saves: {Saves}, participants: {Participants})",
                gameId,
                userId,
                cityResourcesDeleted,
                cityTilesDeleted,
                citiesDeleted,
                unitsDeleted,
                turnsDeleted,
                savesDeleted,
                participantsDeleted);

            // Store successful deletion in idempotency cache if key provided
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var cachedKey = CacheKeys.DeleteGameIdempotency(userId, idempotencyKey);
                await _idempotencyStore.TryStoreAsync(
                    cachedKey,
                    "deleted",
                    TimeSpan.FromHours(24),
                    cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to delete game {GameId} for user {UserId}", gameId, userId);
            throw;
        }
    }

    /// <summary>
    /// Generates a random seed for the RNG.
    /// </summary>
    private static long GenerateRngSeed()
    {
        // Use current timestamp + random component for uniqueness
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = Random.Shared.Next(0, 10000);
        return timestamp + random;
    }
}

