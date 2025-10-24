using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenXEmpires.Server.Domain.Configuration;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Services;
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
            var cachedKey = $"create-game:{userId}:{idempotencyKey}";
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
                var cachedKey = $"create-game:{userId}:{idempotencyKey}";
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

