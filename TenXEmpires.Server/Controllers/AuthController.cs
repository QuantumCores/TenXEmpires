using Asp.Versioning;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Constants;

namespace TenXEmpires.Server.Controllers;

/// <summary>
/// Authentication-related endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/auth")]
[Produces("application/json")]
[ApiExplorerSettings(GroupName = "v1")]
[Tags("Auth")]
[EnableRateLimiting("PublicApi")]
public class AuthController : ControllerBase
{
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAntiforgery antiforgery,
        ILogger<AuthController> logger)
    {
        _antiforgery = antiforgery;
        _logger = logger;
    }

    /// <summary>
    /// Issues or refreshes the CSRF token cookie for the SPA.
    /// </summary>
    /// <remarks>
    /// Sets a non-HttpOnly XSRF-TOKEN cookie that the client should echo via the X-XSRF-TOKEN header
    /// on subsequent non-GET requests protected with ValidateAntiForgeryToken.
    /// </remarks>
    /// <response code="204">Token issued via Set-Cookie (no response body).</response>
    /// <response code="429">Too many requests (rate limited).</response>
    /// <response code="500">Failed to issue token.</response>
    [HttpGet("csrf")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status429TooManyRequests)]
    [SwaggerResponseExample(StatusCodes.Status429TooManyRequests, typeof(TenXEmpires.Server.Examples.ApiErrorRateLimitExample))]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(TenXEmpires.Server.Examples.ApiErrorCsrfIssueFailedExample))]
    public IActionResult GetCsrfToken()
    {
        try
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);

            if (string.IsNullOrWhiteSpace(tokens.RequestToken))
            {
                _logger.LogWarning("Antiforgery returned an empty request token.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { code = "CSRF_ISSUE_FAILED", message = "Unable to issue CSRF token." });
            }

            Response.Cookies.Append(
                SecurityConstants.XsrfCookie,
                tokens.RequestToken!,
                new CookieOptions
                {
                    HttpOnly = false,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });

            // Prevent caching of this response
            Response.Headers[StandardHeaders.CacheControl] = "no-store";

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to issue CSRF token.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { code = "CSRF_ISSUE_FAILED", message = "Unable to issue CSRF token." });
        }
    }
}
