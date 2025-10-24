using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Examples;

namespace TenXEmpires.Server.Controllers;

/// <summary>
/// API controller for unit definitions (static game rules data).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/unit-definitions")]
[Produces("application/json")]
[ApiExplorerSettings(GroupName = "v1")]
[Tags("Unit Definitions")]
[EnableRateLimiting("PublicApi")]
public class UnitDefinitionsController : ControllerBase
{
    private readonly ILookupService _lookupService;
    private readonly ILogger<UnitDefinitionsController> _logger;

    public UnitDefinitionsController(
        ILookupService lookupService,
        ILogger<UnitDefinitionsController> logger)
    {
        _lookupService = lookupService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all unit definitions.
    /// </summary>
    /// <remarks>
    /// Returns the static list of unit types and their stats (attack, defense, movement, etc.).
    /// This data is read-only and cacheable.
    /// 
    /// Supports conditional requests via ETag:
    /// - Response includes an ETag header representing the current data version
    /// - Clients can send If-None-Match header with the ETag value
    /// - If data hasn't changed, returns 304 Not Modified
    /// 
    /// Sample response:
    /// 
    ///     {
    ///       "items": [
    ///         {
    ///           "id": 1,
    ///           "code": "warrior",
    ///           "isRanged": false,
    ///           "attack": 20,
    ///           "defence": 10,
    ///           "rangeMin": 0,
    ///           "rangeMax": 0,
    ///           "movePoints": 2,
    ///           "health": 100
    ///         }
    ///       ]
    ///     }
    /// </remarks>
    /// <response code="200">Returns the list of unit definitions.</response>
    /// <response code="304">Not Modified - cached version is still valid.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpGet(Name = "GetUnitDefinitions")]
    [ProducesResponseType(typeof(ItemsResult<UnitDefinitionDto>), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(UnitDefinitionsDtoExample))]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept")]
    public async Task<ActionResult<ItemsResult<UnitDefinitionDto>>> GetUnitDefinitions()
    {
        try
        {
            _logger.LogDebug("Processing request to get unit definitions");

            // Get current ETag
            var currentETag = await _lookupService.GetUnitDefinitionsETagAsync();

            // Check If-None-Match header for conditional request
            if (Request.Headers.IfNoneMatch.Count > 0)
            {
                var clientETag = Request.Headers.IfNoneMatch.ToString();
                if (clientETag == currentETag)
                {
                    _logger.LogDebug("Client ETag matches current ETag, returning 304 Not Modified");
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            // Fetch data
            var unitDefinitions = await _lookupService.GetUnitDefinitionsAsync();

            var result = new ItemsResult<UnitDefinitionDto>
            {
                Items = unitDefinitions
            };

            // Set caching headers
            Response.Headers.ETag = currentETag;
            Response.Headers.CacheControl = "public, max-age=600"; // 10 minutes

            _logger.LogDebug("Returning {Count} unit definitions with ETag {ETag}", unitDefinitions.Count, currentETag);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve unit definitions");
            
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while retrieving unit definitions."
                });
        }
    }
}

