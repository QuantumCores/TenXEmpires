using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Examples;

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
    private readonly ILogger<GamesController> _logger;

    public GamesController(
        IGameService gameService,
        ILogger<GamesController> logger)
    {
        _gameService = gameService;
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
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Unable to extract user ID from claims");
                return Unauthorized(new
                {
                    code = "UNAUTHORIZED",
                    message = "User authentication is required."
                });
            }

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
}

