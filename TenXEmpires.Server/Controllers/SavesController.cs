using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Examples;
using TenXEmpires.Server.Extensions;

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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
}

