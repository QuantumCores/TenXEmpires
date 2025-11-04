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
using TenXEmpires.Server.Infrastructure.Filters;

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
    private readonly ITurnService _turnService;
    private readonly IActionService _actionService;
    private readonly ILogger<GamesController> _logger;

    public GamesController(
        IGameService gameService,
        IGameStateService gameStateService,
        ITurnService turnService,
        IActionService actionService,
        ILogger<GamesController> logger)
    {
        _gameService = gameService;
        _gameStateService = gameStateService;
        _turnService = turnService;
        _actionService = actionService;
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

            _logger.LogInformation("ARD: query {0}", System.Text.Json.JsonSerializer.Serialize(query));

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
    /// - mapCode (optional): The map code to use (defaults to "standard_15x20" if not provided)
    /// - settings (optional): JSON object with game settings
    /// - displayName (optional): Your display name in the game (defaults to "Player" if not provided)
    /// 
    /// Example request:
    ///
    ///     POST /v1/games
    ///     X-Tenx-Idempotency-Key: unique-request-id-123
    ///     
    ///     {
    ///       "mapCode": "standard_15x20",
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
    [ValidateAntiForgeryTokenApi]
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
    ///       "map": { "id": 1, "code": "standard_15x20", "width": 8, "height": 6 },
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
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(GameStateDtoExample))]
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

    /// <summary>
    /// Gets detailed information for a specific game owned by the authenticated user.
    /// </summary>
    /// <param name="id">The game ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Returns a detailed summary of the game, including metadata like map, turn, status,
    /// and timing fields. If the game does not exist or is not accessible due to RLS, a 404 is returned.
    /// </remarks>
    /// <response code="200">Returns the game details.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="404">Not Found - game does not exist or user doesn't have access.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpGet("{id:long}", Name = "GetGameDetail")]
    [ProducesResponseType(typeof(GameDetailDto), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(GameDetailDtoExample))]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GameDetailDto>> GetGameDetail(
        long id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.GetUserId();

            _logger.LogDebug("User {UserId} requesting detail for game {GameId}", userId, id);

            var detail = await _gameService.GetGameDetailAsync(userId, id, cancellationToken);

            if (detail is null)
            {
                _logger.LogWarning("Game {GameId} not found or user {UserId} doesn't have access", id, userId);
                return NotFound(new
                {
                    code = "GAME_NOT_FOUND",
                    message = "Game not found or you don't have access to it."
                });
            }

            // Conditional GET with ETag based on last turn timestamp and turn number
            var etag = HttpHeaderExtensions.ComposeGameETag(id, detail.TurnNo, detail.LastTurnAt);

            if (Request.IsNotModified(etag))
            {
                Response.SetETag(etag);
                return StatusCode(StatusCodes.Status304NotModified);
            }

            Response.SetETag(etag);
            return Ok(detail);
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
            _logger.LogError(ex, "Failed to get game detail for game {GameId}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while retrieving the game details."
                });
        }
    }

    /// <summary>
    /// Deletes a game and all associated child entities owned by the authenticated user.
    /// </summary>
    /// <param name="id">The game ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Permanently deletes a game and all related entities:
    /// - Participants (human and AI)
    /// - Units and their state
    /// - Cities, city tiles, and city resources
    /// - Game saves (manual and autosave)
    /// - Turn history
    /// 
    /// The operation is transactional - either all entities are deleted or none are.
    /// Only the game owner can delete their games; attempts to delete games owned by others will return 404.
    /// 
    /// The endpoint supports idempotency via the `X-Tenx-Idempotency-Key` header for safe retries.
    /// 
    /// **Warning**: This operation is permanent and cannot be undone.
    /// 
    /// Example:
    ///
    ///     DELETE /v1/games/123
    ///     X-Tenx-Idempotency-Key: unique-request-id-456
    ///
    /// </remarks>
    /// <response code="204">Game successfully deleted.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="404">Not Found - game does not exist or user doesn't have access.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpDelete("{id:long}", Name = "DeleteGame")]
    [ValidateAntiForgeryTokenApi]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    [SwaggerResponseExample(StatusCodes.Status401Unauthorized, typeof(ApiErrorUnauthorizedExample))]
    [SwaggerResponseExample(StatusCodes.Status404NotFound, typeof(ApiErrorGameNotFoundExample))]
    [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(ApiErrorInternalExample))]
    public async Task<IActionResult> DeleteGame(
        long id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get user ID from authenticated user
            var userId = User.GetUserId();

            _logger.LogDebug("User {UserId} attempting to delete game {GameId}", userId, id);

            // Extract idempotency key from headers (optional)
            var idempotencyKey = Request.Headers[TenxHeaders.IdempotencyKey].FirstOrDefault();

            // Call service to delete game
            var deleted = await _gameService.DeleteGameAsync(userId, id, idempotencyKey, cancellationToken);

            if (!deleted)
            {
                _logger.LogWarning("Game {GameId} not found or user {UserId} doesn't have access for deletion", id, userId);
                return NotFound(new
                {
                    code = "GAME_NOT_FOUND",
                    message = "Game not found or you don't have access to it."
                });
            }

            // Return 204 No Content on successful deletion
            return NoContent();
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
            _logger.LogError(ex, "Failed to delete game {GameId}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while deleting the game."
                });
        }
    }

    /// <summary>
    /// Lists committed turns for a specific game with optional sorting and pagination.
    /// </summary>
    /// <param name="id">The game ID.</param>
    /// <param name="query">Query parameters for sorting and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Returns a paginated list of committed turns for the specified game, ordered by turn number or commit timestamp.
    /// This endpoint is useful for displaying game history and timeline views.
    /// 
    /// Query Parameters:
    /// - page: 1-based page number (default: 1)
    /// - pageSize: Number of items per page (default: 20, max: 100)
    /// - sort: Sort field - turnNo or committedAt (default: turnNo)
    /// - order: Sort order - asc or desc (default: desc)
    /// 
    /// Examples:
    /// - GET /v1/games/123/turns - Recent turns first
    /// - GET /v1/games/123/turns?sort=committedAt&amp;order=asc - Chronological order
    /// - GET /v1/games/123/turns?page=2&amp;pageSize=50 - Second page with 50 items
    ///
    /// Sample response:
    ///
    ///     {
    ///       "items": [
    ///         {
    ///           "id": 42,
    ///           "turnNo": 5,
    ///           "participantId": 1,
    ///           "committedAt": "2025-10-20T11:30:00Z",
    ///           "durationMs": 12500,
    ///           "summary": { "actions": 3, "unitsMovedCount": 2 }
    ///         }
    ///       ],
    ///       "page": 1,
    ///       "pageSize": 20,
    ///       "total": 5
    ///     }
    /// </remarks>
    /// <response code="200">Returns the paged list of turns.</response>
    /// <response code="400">Bad request due to invalid query parameters.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="404">Not Found - game does not exist or user doesn't have access.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpGet("{id:long}/turns", Name = "ListGameTurns")]
    [ProducesResponseType(typeof(PagedResult<TurnDto>), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(PagedTurnDtoExample))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResult<TurnDto>>> ListGameTurns(
        long id,
        [FromQuery] ListTurnsQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate model state (DataAnnotations)
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid query parameters for ListGameTurns");
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

            _logger.LogDebug("User {UserId} requesting turns for game {GameId}", userId, id);

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

            // Call service to list turns
            var result = await _turnService.ListTurnsAsync(id, query, cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument for ListGameTurns request");
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
            _logger.LogError(ex, "Failed to list turns for game {GameId}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while retrieving game turns."
                });
        }
    }

    /// <summary>
    /// Moves a unit to a target position in the game.
    /// </summary>
    /// <param name="id">The game ID.</param>
    /// <param name="command">The move command with unit ID and target position.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Moves a unit along a valid path according to deterministic pathfinding rules (A*, uniform cost).
    /// The move must satisfy several constraints:
    /// - It must be the human player's turn (not AI turn)
    /// - The unit must belong to the active participant
    /// - The destination must be reachable within the unit's movement points
    /// - The destination tile must not be occupied by another unit (1UPT - one unit per tile)
    /// - The unit must not have already acted this turn
    /// 
    /// The endpoint supports idempotency via the `X-Tenx-Idempotency-Key` header to safely retry moves.
    /// 
    /// Request Body:
    /// - unitId: The ID of the unit to move
    /// - to: Target position with row and col coordinates
    /// 
    /// Example request:
    ///
    ///     POST /v1/games/42/actions/move
    ///     X-Tenx-Idempotency-Key: unique-move-request-123
    ///     
    ///     {
    ///       "unitId": 201,
    ///       "to": { "row": 2, "col": 3 }
    ///     }
    ///
    /// The response includes the complete updated game state after the move.
    /// </remarks>
    /// <response code="200">Move successful. Returns the updated game state.</response>
    /// <response code="400">Bad request due to invalid input.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="409">Conflict - NOT_PLAYER_TURN, ONE_UNIT_PER_TILE, or NO_ACTIONS_LEFT.</response>
    /// <response code="422">Unprocessable Entity - ILLEGAL_MOVE (path blocked or out of range).</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpPost("{id:long}/actions/move", Name = "MoveUnit")]
    [ValidateAntiForgeryTokenApi]
    [ProducesResponseType(typeof(ActionStateResponse), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(ActionStateResponseExample))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ActionStateResponse>> MoveUnit(
        long id,
        [FromBody] MoveUnitCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate model state
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid request body for MoveUnit on game {GameId}", id);
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

            _logger.LogDebug("User {UserId} attempting to move unit {UnitId} in game {GameId} to ({Row}, {Col})",
                userId, command.UnitId, id, command.To.Row, command.To.Col);

            // Call service to execute move
            var response = await _actionService.MoveUnitAsync(userId, id, command, idempotencyKey, cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not your turn") || ex.Message.Contains("NOT_PLAYER_TURN"))
        {
            _logger.LogWarning(ex, "Not player's turn for game {GameId}", id);
            return Conflict(new
            {
                code = "NOT_PLAYER_TURN",
                message = "It is not your turn to move."
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("occupied") || ex.Message.Contains("ONE_UNIT_PER_TILE"))
        {
            _logger.LogWarning(ex, "Destination tile occupied in game {GameId}", id);
            return Conflict(new
            {
                code = "ONE_UNIT_PER_TILE",
                message = "The destination tile is already occupied by another unit."
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("acted") || ex.Message.Contains("NO_ACTIONS_LEFT"))
        {
            _logger.LogWarning(ex, "Unit has no actions left in game {GameId}", id);
            return Conflict(new
            {
                code = "NO_ACTIONS_LEFT",
                message = "This unit has already acted this turn."
            });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("illegal") || ex.Message.Contains("blocked") || ex.Message.Contains("range"))
        {
            _logger.LogWarning(ex, "Illegal move attempted in game {GameId}", id);
            return UnprocessableEntity(new
            {
                code = "ILLEGAL_MOVE",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("unit"))
        {
            _logger.LogWarning(ex, "Unit not found or doesn't belong to player in game {GameId}", id);
            return NotFound(new
            {
                code = "UNIT_NOT_FOUND",
                message = ex.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt for game {GameId}", id);
            return Unauthorized(new
            {
                code = "UNAUTHORIZED",
                message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument for MoveUnit request on game {GameId}", id);
            return BadRequest(new
            {
                code = "INVALID_INPUT",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move unit in game {GameId}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while processing the move."
                });
        }
    }

    /// <summary>
    /// Executes an attack action from one unit against a target unit using deterministic damage rules.
    /// Ranged attackers never receive a counterattack. Returns updated game state.
    /// </summary>
    /// <param name="id">The game ID.</param>
    /// <param name="command">The attack command with attacker and target unit IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Validations:
    /// - It must be the human player's turn
    /// - Attacker must belong to the active participant and have not acted
    /// - Target must be an enemy unit and be in range (melee: adjacent; ranged: within [min,max])
    /// - Ranged units never receive counterattacks
    /// 
    /// Returns 409 for NOT_PLAYER_TURN or NO_ACTIONS_LEFT, 422 for OUT_OF_RANGE or INVALID_TARGET.
    /// </remarks>
    /// <response code="200">Returns the updated game state after the attack.</response>
    /// <response code="400">Bad request due to invalid payload.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="404">Not Found - unit does not exist or user doesn't have access.</response>
    /// <response code="409">Conflict - NOT_PLAYER_TURN or NO_ACTIONS_LEFT.</response>
    /// <response code="422">Unprocessable Entity - OUT_OF_RANGE or INVALID_TARGET.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpPost("{id:long}/actions/attack", Name = "AttackUnit")]
    [ValidateAntiForgeryTokenApi]
    [SwaggerRequestExample(typeof(AttackUnitCommand), typeof(AttackUnitCommandExample))]
    [ProducesResponseType(typeof(ActionStateResponse), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(ActionStateResponseExample))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ActionStateResponse>> AttackUnit(
        long id,
        [FromBody] AttackUnitCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid request body for AttackUnit on game {GameId}", id);
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

            var userId = User.GetUserId();
            var idempotencyKey = Request.Headers[TenxHeaders.IdempotencyKey].FirstOrDefault();

            _logger.LogDebug("User {UserId} attempting attack in game {GameId} (attacker {AttackerId} -> target {TargetId})",
                userId, id, command.AttackerUnitId, command.TargetUnitId);

            var response = await _actionService.AttackAsync(userId, id, command, idempotencyKey, cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("NOT_PLAYER_TURN"))
        {
            _logger.LogWarning(ex, "Not player's turn for game {GameId}", id);
            return Conflict(new { code = "NOT_PLAYER_TURN", message = "It is not your turn to act." });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("NO_ACTIONS_LEFT"))
        {
            _logger.LogWarning(ex, "Unit has no actions left in game {GameId}", id);
            return Conflict(new { code = "NO_ACTIONS_LEFT", message = "This unit has already acted this turn." });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("OUT_OF_RANGE"))
        {
            _logger.LogWarning(ex, "Attack out of range in game {GameId}", id);
            return UnprocessableEntity(new { code = "OUT_OF_RANGE", message = ex.Message });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("INVALID_TARGET"))
        {
            _logger.LogWarning(ex, "Invalid attack target in game {GameId}", id);
            return UnprocessableEntity(new { code = "INVALID_TARGET", message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("UNIT_NOT_FOUND"))
        {
            _logger.LogWarning(ex, "Unit not found or doesn't belong to player in game {GameId}", id);
            return NotFound(new { code = "UNIT_NOT_FOUND", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt for game {GameId}", id);
            return Unauthorized(new { code = "UNAUTHORIZED", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument for AttackUnit request on game {GameId}", id);
            return BadRequest(new { code = "INVALID_INPUT", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute attack in game {GameId}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { code = "INTERNAL_ERROR", message = "An error occurred while processing the attack." });
        }
    }

    /// <summary>
    /// Ends the active participant's turn, commits the turn, creates an autosave, and advances to the next participant.
    /// </summary>
    /// <param name="id">The game ID.</param>
    /// <param name="command">Optional empty command body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Triggers end-of-turn systems (regen/harvest/production), writes a Turn record, creates an autosave, and advances turn/participant.
    /// If the next participant is AI, it may be executed within budgeted time in later steps.
    /// </remarks>
    /// <response code="200">Returns the updated state with turn summary and autosave id.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="409">Conflict - NOT_PLAYER_TURN or TURN_IN_PROGRESS.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpPost("{id:long}/end-turn", Name = "EndTurn")]
    [ValidateAntiForgeryTokenApi]
    [ProducesResponseType(typeof(EndTurnResponse), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(EndTurnResponseExample))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EndTurnResponse>> EndTurn(
        long id,
        [FromBody] EndTurnCommand? command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.GetUserId();
            var idempotencyKey = Request.Headers[TenxHeaders.IdempotencyKey].FirstOrDefault();

            _logger.LogDebug("User {UserId} ending turn for game {GameId}", userId, id);

            var response = await _turnService.EndTurnAsync(
                userId,
                id,
                command ?? new EndTurnCommand(),
                idempotencyKey,
                cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("NOT_PLAYER_TURN", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("not your turn", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Not player's turn for game {GameId}", id);
            return Conflict(new { code = "NOT_PLAYER_TURN", message = "It is not your turn." });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("TURN_IN_PROGRESS", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("in progress", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Turn already in progress for game {GameId}", id);
            return Conflict(new { code = "TURN_IN_PROGRESS", message = "A turn action is already in progress. Please wait and retry." });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt for game {GameId}", id);
            return Unauthorized(new { code = "UNAUTHORIZED", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument for EndTurn on game {GameId}", id);
            return BadRequest(new { code = "INVALID_INPUT", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to end turn for game {GameId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { code = "INTERNAL_ERROR", message = "An error occurred while ending the turn." });
        }
    }
}

