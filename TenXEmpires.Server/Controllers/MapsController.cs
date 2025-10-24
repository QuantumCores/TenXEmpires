using Asp.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;

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
            var etag = ComputeMapETag(map);

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

    private static string ComputeMapETag(MapDto map)
    {
        var representation = $"{map.Code}:{map.SchemaVersion}:{map.Width}:{map.Height}";
        var bytes = Encoding.UTF8.GetBytes(representation);
        var hash = SHA256.HashData(bytes);
        var etag = Convert.ToBase64String(hash);
        return $"\"{etag}\""; // quoted per RFC 7232
    }
}
