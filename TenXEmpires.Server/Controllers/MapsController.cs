using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Examples;

namespace TenXEmpires.Server.Controllers;

/// <summary>
/// API controller for map metadata lookups.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/maps")]
[Produces("application/json")]
[ApiExplorerSettings(GroupName = "v1")]
[Tags("Maps")]
[EnableRateLimiting("PublicApi")]
public class MapsController : ControllerBase
{
    private readonly ILookupService _lookupService;
    private readonly ILogger<MapsController> _logger;

    public MapsController(
        ILookupService lookupService,
        ILogger<MapsController> logger)
    {
        _lookupService = lookupService;
        _logger = logger;
    }

    /// <summary>
    /// Gets metadata for a specific map by code.
    /// </summary>
    /// <param name="code">The unique map code.</param>
    /// <remarks>
    /// Supports conditional requests via ETag:
    /// - Response includes an ETag header derived from schemaVersion, width, height
    /// - Clients can send If-None-Match header with the ETag value
    /// - If data hasn't changed, returns 304 Not Modified
    ///
    /// Sample response:
    ///
    ///     {
    ///       "id": 1,
    ///       "code": "map-01",
    ///       "schemaVersion": 1,
    ///       "width": 20,
    ///       "height": 30
    ///     }
    /// </remarks>
    /// <response code="200">Returns the map metadata.</response>
    /// <response code="400">Bad request due to invalid code parameter.</response>
    /// <response code="404">Map not found.</response>
    /// <response code="304">Not Modified - cached version is still valid.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpGet("{code}", Name = "GetMapByCode")]
    [ProducesResponseType(typeof(MapDto), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(MapDtoExample))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept")]
    public async Task<ActionResult<MapDto>> GetMapByCode(string code)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                _logger.LogWarning("Invalid map code provided");
                return BadRequest(new
                {
                    code = "INVALID_CODE",
                    message = "The 'code' path parameter must be provided."
                });
            }

            // Normalize code if needed (currently case-sensitive)
            var map = await _lookupService.GetMapByCodeAsync(code);
            if (map is null)
            {
                _logger.LogInformation("Map with code {Code} not found", code);
                return NotFound(new
                {
                    code = "MAP_NOT_FOUND",
                    message = $"Map with code '{code}' was not found."
                });
            }

            // Compute ETag based on stable map metadata
            var etag = _lookupService.ComputeMapETag(map);

            // Conditional request handling
            if (Request.Headers.IfNoneMatch.Count > 0)
            {
                var clientETag = Request.Headers.IfNoneMatch.ToString();
                if (clientETag == etag)
                {
                    Response.Headers.ETag = etag;
                    _logger.LogDebug("Client ETag matches current ETag for map {Code}; returning 304.", code);
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            // Set caching headers
            Response.Headers.ETag = etag;
            Response.Headers.CacheControl = "public, max-age=600"; // 10 minutes

            return Ok(map);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve map by code {Code}", code);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while retrieving the map."
                });
        }
    }

    /// <summary>
    /// Gets the list of tiles for a specific map by code.
    /// </summary>
    /// <param name="code">The unique map code.</param>
    /// <param name="page">Optional 1-based page number (default: 1).</param>
    /// <param name="pageSize">Optional page size (default: 20, max: 100).</param>
    /// <remarks>
    /// Returns tiles for rendering terrain and resources. Supports pagination for large maps.
    /// Results are ordered by row, then column for stable pagination.
    /// 
    /// Supports conditional requests via ETag:
    /// - Response includes an ETag header derived from map code, pagination, and tile count
    /// - Clients can send If-None-Match header with the ETag value
    /// - If data hasn't changed, returns 304 Not Modified
    ///
    /// Sample response:
    ///
    ///     {
    ///       "items": [
    ///         {
    ///           "id": 1,
    ///           "row": 0,
    ///           "col": 0,
    ///           "terrain": "grassland",
    ///           "resourceType": "wheat",
    ///           "resourceAmount": 2
    ///         }
    ///       ],
    ///       "page": 1,
    ///       "pageSize": 20,
    ///       "total": 400
    ///     }
    /// </remarks>
    /// <response code="200">Returns the paged list of map tiles.</response>
    /// <response code="400">Bad request due to invalid parameters.</response>
    /// <response code="404">Map not found.</response>
    /// <response code="304">Not Modified - cached version is still valid.</response>
    /// <response code="500">Internal server error occurred.</response>
    [HttpGet("{code}/tiles", Name = "GetMapTiles")]
    [ProducesResponseType(typeof(PagedResult<MapTileDto>), StatusCodes.Status200OK)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(PagedMapTileDtoExample))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "page", "pageSize" })]
    public async Task<ActionResult<PagedResult<MapTileDto>>> GetMapTiles(
        string code,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        try
        {
            // Validate map code
            if (string.IsNullOrWhiteSpace(code))
            {
                _logger.LogWarning("Invalid map code provided");
                return BadRequest(new
                {
                    code = "INVALID_CODE",
                    message = "The 'code' path parameter must be provided."
                });
            }

            // Validate pagination parameters
            var effectivePage = page ?? 1;
            var effectivePageSize = pageSize ?? 20;

            if (effectivePage < 1)
            {
                _logger.LogWarning("Invalid page number: {Page}", page);
                return BadRequest(new
                {
                    code = "INVALID_PAGE",
                    message = "The 'page' parameter must be >= 1."
                });
            }

            if (effectivePageSize < 1 || effectivePageSize > 100)
            {
                _logger.LogWarning("Invalid page size: {PageSize}", pageSize);
                return BadRequest(new
                {
                    code = "INVALID_PAGE_SIZE",
                    message = "The 'pageSize' parameter must be between 1 and 100."
                });
            }

            // Fetch tiles from service
            var tiles = await _lookupService.GetMapTilesAsync(code, effectivePage, effectivePageSize);
            if (tiles is null)
            {
                _logger.LogInformation("Map with code {Code} not found", code);
                return NotFound(new
                {
                    code = "MAP_NOT_FOUND",
                    message = $"Map with code '{code}' was not found."
                });
            }

            // Get ETag for conditional requests
            var etag = await _lookupService.GetMapTilesETagAsync(code, effectivePage, effectivePageSize);
            if (etag is not null)
            {
                // Conditional request handling
                if (Request.Headers.IfNoneMatch.Count > 0)
                {
                    var clientETag = Request.Headers.IfNoneMatch.ToString();
                    if (clientETag == etag)
                    {
                        Response.Headers.ETag = etag;
                        _logger.LogDebug(
                            "Client ETag matches current ETag for map {Code} tiles (page {Page}, size {PageSize}); returning 304.",
                            code,
                            effectivePage,
                            effectivePageSize);
                        return StatusCode(StatusCodes.Status304NotModified);
                    }
                }

                // Set ETag header
                Response.Headers.ETag = etag;
            }

            // Set caching headers
            Response.Headers.CacheControl = "public, max-age=600"; // 10 minutes

            return Ok(tiles);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument for map tiles request");
            return BadRequest(new
            {
                code = "INVALID_ARGUMENT",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve map tiles for code {Code}", code);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while retrieving the map tiles."
                });
        }
    }
}
