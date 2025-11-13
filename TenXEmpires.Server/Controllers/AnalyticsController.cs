using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Extensions;

namespace TenXEmpires.Server.Controllers;

/// <summary>
/// API controller for ingesting first-party analytics events.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/analytics")]
[Produces("application/json")]
[ApiExplorerSettings(GroupName = "v1")]
[Tags("Analytics")]
[EnableRateLimiting("AnalyticsIngest")]
[AllowAnonymous]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(IAnalyticsService analyticsService, ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>
    /// Ingests a batch of analytics events with privacy-preserving pseudonymization and best-effort deduplication.
    /// </summary>
    /// <param name="command">Batch of analytics events.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="202">Accepted with count of ingested events.</response>
    /// <response code="400">Invalid payload.</response>
    /// <response code="429">Too many requests (rate-limited per identity).</response>
    /// <response code="500">Server error.</response>
    [HttpPost("batch")]
    [SwaggerRequestExample(typeof(AnalyticsBatchCommand), typeof(TenXEmpires.Server.Examples.AnalyticsBatchCommandExample))]
    [ProducesResponseType(typeof(AnalyticsBatchResponse), StatusCodes.Status202Accepted)]
    [SwaggerResponseExample(StatusCodes.Status202Accepted, typeof(TenXEmpires.Server.Examples.AnalyticsBatchResponseExample))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalyticsBatchResponse>> IngestBatch(
        [FromBody] AnalyticsBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (command is null || command.Events is null)
            {
                return BadRequest(new { code = "INVALID_INPUT", message = "Request body is required." });
            }

            // Validate items
            var errors = new List<string>();

            if (command.Events.Count == 0)
            {
                errors.Add("At least one event is required.");
            }

            for (var i = 0; i < command.Events.Count; i++)
            {
                var item = command.Events[i];

                if (string.IsNullOrWhiteSpace(item.EventType))
                {
                    errors.Add($"events[{i}].eventType is required.");
                }
                else if (!AnalyticsEventTypes.IsValid(item.EventType))
                {
                    errors.Add($"events[{i}].eventType is invalid. Use one of: game_start, turn_end, autosave, manual_save, manual_load, game_finish or prefix custom.*");
                }

                if (item.GameId is { } gid && gid < 0)
                {
                    errors.Add($"events[{i}].gameId must be positive.");
                }

                if (!string.IsNullOrWhiteSpace(item.ClientRequestId) && !Guid.TryParse(item.ClientRequestId, out _))
                {
                    errors.Add($"events[{i}].clientRequestId must be a valid GUID.");
                }
            }

            if (errors.Count > 0)
            {
                _logger.LogWarning("Invalid analytics batch payload: {Errors}", string.Join("; ", errors));
                return BadRequest(new { code = "INVALID_INPUT", message = "Validation failed.", errors });
            }

            // Try extract identity info
            Guid? userId = null;
            if (User.TryGetUserId(out var uid))
            {
                userId = uid;
            }

            // Prefer explicit session cookie for anonymous identity if present
            var deviceId = Request.Cookies["tenx.sid"];

            var accepted = await _analyticsService.IngestBatchAsync(userId, deviceId, command, cancellationToken);

            return Accepted(new AnalyticsBatchResponse(accepted));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analytics batch ingestion failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { code = "INTERNAL_ERROR", message = "Failed to ingest analytics batch." });
        }
    }
}
