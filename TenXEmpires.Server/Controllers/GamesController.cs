using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Examples;
using TenXEmpires.Server.Extensions;

namespace TenXEmpires.Server.Controllers;

/// <summary>
/// API controller for game management.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/games")]
[Produces("application/json")]
[ApiExplorerSettings(GroupName = "v1")]
[Tags("Games")]
[EnableRateLimiting("AuthenticatedApi")]
[Authorize]
public class GamesController : ControllerBase
{
    private readonly IGameService _gameService;
    private readonly IGameStateService _gameStateService;
    private readonly ILogger<GamesController> _logger;

    public GamesController(
        IGameService gameService,
        IGameStateService gameStateService,
        ILogger<GamesController> logger)
    {
        _gameService = gameService;
        _gameStateService = gameStateService;
        _logger = logger;
    }

    /// <summary>
    /// Lists the authenticated user's games with optional filtering and pagination.
    /// </summary>
    /// <param name="query">Query parameters for filtering, sorting, and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Returns a paginated list of games for the authenticated user.
    /// RLS (Row-Level Security) is enforced via the session `app.user_id` context variable.
    /// 
    /// Query Parameters:
    /// - status: Filter by game status (active or finished)
    /// - page: 1-based page number (default: 1)
    /// - pageSize: Number of items per page (default: 20, max: 100)
    /// - sort: Sort field - startedAt, lastTurnAt, or turnNo (default: lastTurnAt)
    /// - order: Sort order - asc or desc (default: desc)
    /// 
    /// Examples:
    /// - GET /v1/games - All games, sorted by last turn descending
    /// - GET /v1/games?status=active - Active games only
    /// - GET /v1/games?sort=startedAt&amp;order=asc&amp;page=2&amp;pageSize=10 - Second page, sorted by start date ascending
    ///
    /// Sample response:
    ///
    ///     {
    ///       "items": [
    ///         {
    ///           "id": 1,
    ///           "status": "active",
    ///           "turnNo": 5,
    ///           "mapId": 1,
    ///           "mapSchemaVersion": 1,
    ///           "startedAt": "2025-10-20T10:00:00Z",
    ///           "finishedAt": null,
    ///           "lastTurnAt": "2025-10-20T11:30:00Z"
    ///         }
    ///       ],
    ///       "page": 1,
    ///       "pageSize": 20,
    ///       "total": 15
    ///     }
    /// </remarks>
    /// <response code="200">Returns the paged list of games.</response>
    /// <response code="400">Bad request due to invalid query parameters.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpGet(Name = "ListGames")]
    [ProducesResponseType(typeof(PagedResult<GameListItemDto>), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(PagedGameListItemDtoExample))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResult<GameListItemDto>>> ListGames(
        [FromQuery] ListGamesQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate model state (DataAnnotations)
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid query parameters for ListGames");
                return BadRequest(new
                {
                    code = "INVALID_INPUT",
                    message = "One or more validation errors occurred.",
                    errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });
            }

            // Get user ID from authenticated user
            var userId = User.GetUserId();

            // Call service to list games
            var result = await _gameService.ListGamesAsync(userId, query, cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument for ListGames request");
            return BadRequest(new
            {
                code = "INVALID_INPUT",
                message = ex.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt");
            return Unauthorized(new
            {
                code = "UNAUTHORIZED",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list games");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while retrieving games."
                });
        }
    }

    /// <summary>
    /// Creates a new game for the authenticated user.
    /// </summary>
    /// <param name="command">The command to create a new game with optional map code, settings, and display name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Creates a new game instance on a fixed map, initializes participants (human + AI),
    /// seeds starting cities/units, and returns the initial game state.
    /// 
    /// The endpoint supports idempotency via the `X-Tenx-Idempotency-Key` header to prevent duplicate game creation.
    /// 
    /// Request Body:
    /// - mapCode (optional): The map code to use (defaults to "standard_6x8" if not provided)
    /// - settings (optional): JSON object with game settings
    /// - displayName (optional): Your display name in the game (defaults to "Player" if not provided)
    /// 
    /// Example request:
    ///
    ///     POST /v1/games
    ///     X-Tenx-Idempotency-Key: unique-request-id-123
    ///     
    ///     {
    ///       "mapCode": "standard_6x8",
    ///       "settings": { "difficulty": "normal" },
    ///       "displayName": "Commander Alex"
    ///     }
    ///
    /// The response includes the full initial game state with all entities (participants, cities, units).
    /// Your AI opponent will be assigned a random historical or fantasy leader name (e.g., "Charlemagne", "Cyrus the Great").
    /// </remarks>
    /// <response code="201">Game created successfully. Returns the game ID and initial state.</response>
    /// <response code="400">Bad request due to invalid input.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="409">Conflict - user has reached the game limit.</response>
    /// <response code="422">Unprocessable Entity - map schema mismatch or map not found.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpPost(Name = "CreateGame")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(GameCreatedResponse), StatusCodes.Status201Created)]
    [SwaggerResponseExample(StatusCodes.Status201Created, typeof(GameCreatedResponseExample))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GameCreatedResponse>> CreateGame(
        [FromBody] CreateGameCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate model state
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid request body for CreateGame");
                return BadRequest(new
                {
                    code = "INVALID_INPUT",
                    message = "One or more validation errors occurred.",
                    errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });
            }

            // Get user ID from authenticated user
            var userId = User.GetUserId();

            // Extract idempotency key from headers (optional)
            var idempotencyKey = Request.Headers[TenxHeaders.IdempotencyKey].FirstOrDefault();

            // Call service to create game
            var response = await _gameService.CreateGameAsync(userId, command, idempotencyKey, cancellationToken);

            // Return 201 Created with Location header
            return CreatedAtRoute(
                "GetGameState",
                new { id = response.Id },
                response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("limit"))
        {
            _logger.LogWarning(ex, "Game limit reached for user");
            return Conflict(new
            {
                code = "GAME_LIMIT_REACHED",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("map") || ex.Message.Contains("schema"))
        {
            _logger.LogWarning(ex, "Map schema mismatch or map not found");
            return UnprocessableEntity(new
            {
                code = "MAP_SCHEMA_MISMATCH",
                message = ex.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt");
            return Unauthorized(new
            {
                code = "UNAUTHORIZED",
                message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument for CreateGame request");
            return BadRequest(new
            {
                code = "INVALID_INPUT",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create game");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while creating the game."
                });
        }
    }

    /// <summary>
    /// Gets the current state of a specific game.
    /// </summary>
    /// <param name="id">The game ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Returns the complete game state including all participants, cities, units, resources, and unit definitions.
    /// This is the authoritative game state used by clients to render the game.
    /// 
    /// The response includes:
    /// - Game metadata (turn number, active player, status)
    /// - Map information
    /// - All participants and their states
    /// - All units with positions and stats
    /// - All cities with positions and resources
    /// - Unit definitions (for client reference)
    /// 
    /// Example response structure:
    ///
    ///     {
    ///       "game": { "id": 1, "turnNo": 1, "activeParticipantId": 1, "status": "active" },
    ///       "map": { "id": 1, "code": "standard_6x8", "width": 8, "height": 6 },
    ///       "participants": [...],
    ///       "units": [...],
    ///       "cities": [...],
    ///       "cityTiles": [...],
    ///       "cityResources": [...],
    ///       "unitDefinitions": [...]
    ///     }
    /// </remarks>
    /// <response code="200">Returns the current game state.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="404">Not Found - game does not exist or user doesn't have access.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpGet("{id}/state", Name = "GetGameState")]
    [ProducesResponseType(typeof(GameStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GameStateDto>> GetGameState(
        long id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get user ID from authenticated user
            var userId = User.GetUserId();

            _logger.LogDebug("User {UserId} requesting state for game {GameId}", userId, id);

            // Verify the game exists and belongs to the user
            var gameExists = await _gameService.VerifyGameAccessAsync(userId, id, cancellationToken);

            if (!gameExists)
            {
                _logger.LogWarning("Game {GameId} not found or user {UserId} doesn't have access", id, userId);
                return NotFound(new
                {
                    code = "GAME_NOT_FOUND",
                    message = "Game not found or you don't have access to it."
                });
            }

            // Build and return game state
            var gameState = await _gameStateService.BuildGameStateAsync(id, cancellationToken);

            return Ok(gameState);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt");
            return Unauthorized(new
            {
                code = "UNAUTHORIZED",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Game {GameId} not found", id);
            return NotFound(new
            {
                code = "GAME_NOT_FOUND",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get game state for game {GameId}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while retrieving the game state."
                });
        }
    }
}

