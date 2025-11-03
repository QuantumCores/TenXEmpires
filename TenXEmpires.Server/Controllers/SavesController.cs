using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Examples;
using TenXEmpires.Server.Extensions;
using TenXEmpires.Server.Infrastructure.Filters;

namespace TenXEmpires.Server.Controllers;

/// <summary>
/// API controller for game saves management.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/games")]
[Produces("application/json")]
[ApiExplorerSettings(GroupName = "v1")]
[Tags("Saves")]
[EnableRateLimiting("AuthenticatedApi")]
[Authorize]
public class SavesController : ControllerBase
{
    private readonly ISaveService _saveService;
    private readonly IGameService _gameService;
    private readonly ILogger<SavesController> _logger;

    public SavesController(
        ISaveService saveService,
        IGameService gameService,
        ILogger<SavesController> logger)
    {
        _saveService = saveService;
        _gameService = gameService;
        _logger = logger;
    }

    /// <summary>
    /// Lists all saves (manual and autosaves) for a specific game.
    /// </summary>
    /// <param name="id">The game ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Returns two lists: manual saves (slots 1-3) and autosaves (most recent first, max 5 shown).
    /// RLS (Row-Level Security) is enforced via the session `app.user_id` context variable.
    /// 
    /// Manual saves are user-created snapshots with custom names.
    /// Autosaves are created automatically at the end of each turn.
    /// 
    /// Example response:
    ///
    ///     {
    ///       "manual": [
    ///         {
    ///           "id": 101,
    ///           "slot": 1,
    ///           "turnNo": 5,
    ///           "createdAt": "2025-10-20T11:30:00Z",
    ///           "name": "Before attacking Rome"
    ///         }
    ///       ],
    ///       "autosaves": [
    ///         {
    ///           "id": 201,
    ///           "turnNo": 7,
    ///           "createdAt": "2025-10-20T14:15:00Z"
    ///         },
    ///         {
    ///           "id": 202,
    ///           "turnNo": 6,
    ///           "createdAt": "2025-10-20T13:45:00Z"
    ///         }
    ///       ]
    ///     }
    /// </remarks>
    /// <response code="200">Returns the list of saves.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="404">Not Found - game does not exist or user doesn't have access.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpGet("{id:long}/saves", Name = "ListGameSaves")]
    [ProducesResponseType(typeof(GameSavesListDto), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(GameSavesListDtoExample))]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    [SwaggerResponseExample(StatusCodes.Status401Unauthorized, typeof(ApiErrorUnauthorizedExample))]
    [SwaggerResponseExample(StatusCodes.Status404NotFound, typeof(ApiErrorGameNotFoundExample))]
    [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(ApiErrorInternalExample))]
    public async Task<ActionResult<GameSavesListDto>> ListGameSaves(
        long id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get user ID from authenticated user
            var userId = User.GetUserId();

            _logger.LogDebug("User {UserId} requesting saves for game {GameId}", userId, id);

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

            // List saves
            var saves = await _saveService.ListSavesAsync(id, cancellationToken);

            return Ok(saves);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list saves for game {GameId}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while retrieving saves."
                });
        }
    }

    /// <summary>
    /// Creates or overwrites a manual save in a slot (1..3) for the specified game and current turn.
    /// </summary>
    /// <param name="id">The game ID.</param>
    /// <param name="command">The manual save command with slot and name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Creates a manual save for the current game state in the specified slot. If a manual save already exists
    /// in that slot, it is overwritten. Returns the created save metadata.
    /// 
    /// Supports idempotency via the `X-Tenx-Idempotency-Key` header for safe retries.
    /// </remarks>
    /// <response code="201">Manual save created or overwritten successfully.</response>
    /// <response code="400">Bad request due to invalid slot or name.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="404">Not Found - game does not exist or user doesn't have access.</response>
    /// <response code="409">Conflict - SAVE_CONFLICT on upsert failure.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpPost("{id:long}/saves/manual", Name = "CreateManualSave")]
    [ValidateAntiForgeryTokenApi]
    [SwaggerRequestExample(typeof(CreateManualSaveCommand), typeof(CreateManualSaveCommandExample))]
    [ProducesResponseType(typeof(SaveCreatedDto), StatusCodes.Status201Created)]
    [SwaggerResponseExample(StatusCodes.Status201Created, typeof(SaveCreatedDtoExample))]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(ApiErrorInvalidInputExample))]
    [SwaggerResponseExample(StatusCodes.Status401Unauthorized, typeof(ApiErrorUnauthorizedExample))]
    [SwaggerResponseExample(StatusCodes.Status404NotFound, typeof(ApiErrorGameNotFoundExample))]
    [SwaggerResponseExample(StatusCodes.Status409Conflict, typeof(ApiErrorSaveConflictExample))]
    [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(ApiErrorInternalExample))]
    public async Task<ActionResult<SaveCreatedDto>> CreateManualSave(
        long id,
        [FromBody] CreateManualSaveCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid request body for CreateManualSave on game {GameId}", id);
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

            if (command.Slot < 1 || command.Slot > 3)
            {
                return BadRequest(new { code = "INVALID_SLOT", message = "Slot must be between 1 and 3." });
            }

            if (string.IsNullOrWhiteSpace(command.Name))
            {
                return BadRequest(new { code = "INVALID_NAME", message = "Name cannot be empty." });
            }

            var userId = User.GetUserId();

            // Verify access
            var gameExists = await _gameService.VerifyGameAccessAsync(userId, id, cancellationToken);
            if (!gameExists)
            {
                _logger.LogWarning("Game {GameId} not found or user {UserId} doesn't have access", id, userId);
                return NotFound(new { code = "GAME_NOT_FOUND", message = "Game not found or you don't have access to it." });
            }

            var idempotencyKey = Request.Headers[TenxHeaders.IdempotencyKey].FirstOrDefault();

            var result = await _saveService.CreateManualAsync(userId, id, command, idempotencyKey, cancellationToken);

            return CreatedAtRoute("ListGameSaves", new { id }, result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("SAVE_CONFLICT", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Manual save conflict for game {GameId}", id);
            return Conflict(new { code = "SAVE_CONFLICT", message = "Could not create manual save due to a conflict. Please retry." });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument for CreateManualSave on game {GameId}", id);
            return BadRequest(new { code = "INVALID_INPUT", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt for game {GameId}", id);
            return Unauthorized(new { code = "UNAUTHORIZED", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create manual save for game {GameId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { code = "INTERNAL_ERROR", message = "An error occurred while creating the manual save." });
        }
    }

    /// <summary>
    /// Deletes a manual save in the specified slot (1..3) for a game.
    /// </summary>
    /// <param name="id">The game ID.</param>
    /// <param name="slot">The manual save slot to delete (1..3).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Deletes the manual save for the given slot if it exists and the authenticated user has access
    /// to the game. Returns 204 No Content on success. Returns 404 if the game or the manual save
    /// in the specified slot does not exist or is not accessible.
    ///
    /// Supports idempotency via the `X-Tenx-Idempotency-Key` header for safe retries.
    /// </remarks>
    /// <response code="204">Manual save deleted successfully.</response>
    /// <response code="400">Bad request due to invalid slot.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="404">Not Found - game or manual save does not exist or user doesn't have access.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpDelete("{id:long}/saves/manual/{slot:int}", Name = "DeleteManualSave")]
    [ValidateAntiForgeryTokenApi]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(ApiErrorInvalidSlotExample))]
    [SwaggerResponseExample(StatusCodes.Status401Unauthorized, typeof(ApiErrorUnauthorizedExample))]
    [SwaggerResponseExample(StatusCodes.Status404NotFound, typeof(ApiErrorManualSaveNotFoundExample))]
    [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(ApiErrorInternalExample))]
    public async Task<IActionResult> DeleteManualSave(
        long id,
        int slot,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (slot < 1 || slot > 3)
            {
                return BadRequest(new { code = "INVALID_SLOT", message = "Slot must be between 1 and 3." });
            }

            var userId = User.GetUserId();

            // Verify the game exists and belongs to the user
            var gameExists = await _gameService.VerifyGameAccessAsync(userId, id, cancellationToken);
            if (!gameExists)
            {
                _logger.LogWarning("Game {GameId} not found or user {UserId} doesn't have access", id, userId);
                return NotFound(new { code = "GAME_NOT_FOUND", message = "Game not found or you don't have access to it." });
            }

            var idempotencyKey = Request.Headers[TenxHeaders.IdempotencyKey].FirstOrDefault();

            var deleted = await _saveService.DeleteManualAsync(id, slot, idempotencyKey, cancellationToken);
            if (!deleted)
            {
                _logger.LogWarning("Manual save not found for game {GameId} slot {Slot}", id, slot);
                return NotFound(new { code = "SAVE_NOT_FOUND", message = "Manual save not found in the specified slot." });
            }

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument for DeleteManualSave on game {GameId}", id);
            return BadRequest(new { code = "INVALID_INPUT", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt for game {GameId}", id);
            return Unauthorized(new { code = "UNAUTHORIZED", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete manual save for game {GameId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { code = "INTERNAL_ERROR", message = "An error occurred while deleting the manual save." });
        }
    }

    /// <summary>
    /// Loads a save into its game, replacing current state with the saved snapshot.
    /// </summary>
    /// <param name="saveId">The save ID to load.</param>
    /// <param name="command">Empty command body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Loads a save into its game, replacing current state with the saved snapshot, subject to schema/version compatibility checks.
    /// Returns updated GameState and game id.
    /// 
    /// Supports idempotency via the `X-Tenx-Idempotency-Key` header for safe retries.
    /// 
    /// The operation validates:
    /// - Ownership via RLS (save must belong to authenticated user via game ownership)
    /// - Schema version compatibility between save and current game state
    /// 
    /// Example request:
    ///
    ///     POST /v1/saves/123/load
    ///     X-Tenx-Idempotency-Key: unique-request-id-789
    ///     
    ///     {}
    ///
    /// The response includes the complete game state after loading the save.
    /// </remarks>
    /// <response code="200">Save loaded successfully. Returns the game ID and updated state.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="403">Forbidden - save does not belong to user via game ownership.</response>
    /// <response code="404">Not Found - save does not exist.</response>
    /// <response code="422">Unprocessable Entity - schema version mismatch.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpPost("/v{version:apiVersion}/saves/{saveId:long}/load", Name = "LoadSave")]
    [ValidateAntiForgeryTokenApi]
    [ProducesResponseType(typeof(LoadSaveResponse), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(LoadSaveResponseExample))]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    [SwaggerResponseExample(StatusCodes.Status401Unauthorized, typeof(ApiErrorUnauthorizedExample))]
    [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(ApiErrorInternalExample))]
    public async Task<ActionResult<LoadSaveResponse>> LoadSave(
        long saveId,
        [FromBody] LoadSaveCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.GetUserId();

            _logger.LogDebug("User {UserId} requesting to load save {SaveId}", userId, saveId);

            var idempotencyKey = Request.Headers[TenxHeaders.IdempotencyKey].FirstOrDefault();

            var result = await _saveService.LoadAsync(userId, saveId, idempotencyKey, cancellationToken);

            _logger.LogInformation("Successfully loaded save {SaveId} into game {GameId} for user {UserId}", 
                saveId, result.GameId, userId);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Save {SaveId} not found", saveId);
            return NotFound(new { code = "SAVE_NOT_FOUND", message = "Save not found." });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt for save {SaveId}", saveId);
            return StatusCode(StatusCodes.Status403Forbidden, 
                new { code = "FORBIDDEN", message = "You don't have access to this save." });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("SCHEMA_MISMATCH", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Schema mismatch when loading save {SaveId}", saveId);
            return UnprocessableEntity(new { code = "SCHEMA_MISMATCH", message = "Save schema version is incompatible with current game schema." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to load save {SaveId}", saveId);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { code = "INTERNAL_ERROR", message = "An error occurred while loading the save." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while loading save {SaveId}", saveId);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { code = "INTERNAL_ERROR", message = "An error occurred while loading the save." });
        }
    }
}

