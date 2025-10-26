using Asp.Versioning;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
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

    /// <summary>
    /// Refreshes the authenticated session to extend sliding expiration.
    /// </summary>
    /// <remarks>
    /// No response body. Used by the UI idle banner when the user opts to stay signed in.
    /// Response is marked no-store to prevent caching.
    /// </remarks>
    /// <response code="204">Session refreshed (or already valid). No content.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="429">Too many requests (rate limited).</response>
    /// <response code="500">Unable to refresh session.</response>
    [HttpGet("keepalive")]
    [Authorize]
    [EnableRateLimiting("AuthenticatedApi")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status401Unauthorized)]
    [SwaggerResponseExample(StatusCodes.Status401Unauthorized, typeof(TenXEmpires.Server.Examples.ApiErrorUnauthorizedExample))]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status429TooManyRequests)]
    [SwaggerResponseExample(StatusCodes.Status429TooManyRequests, typeof(TenXEmpires.Server.Examples.ApiErrorRateLimitExample))]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(TenXEmpires.Server.Examples.ApiErrorKeepAliveFailedExample))]
    public async Task<IActionResult> KeepAlive()
    {
        try
        {
            // Mark response as non-cacheable
            Response.Headers[StandardHeaders.CacheControl] = "no-store";

            // If ASP.NET Identity is configured, proactively extend cookie lifetime.
            if (HttpContext.User?.Identity?.IsAuthenticated == true)
            {
                var signInManager = HttpContext.RequestServices.GetService(typeof(SignInManager<IdentityUser<Guid>>)) as SignInManager<IdentityUser<Guid>>;
                if (signInManager is not null)
                {
                    var user = await signInManager.UserManager.GetUserAsync(User);
                    if (user is not null)
                    {
                        await signInManager.RefreshSignInAsync(user);
                    }
                    else
                    {
                        // Fallback: re-issue cookie for current principal
                        var authService = HttpContext.RequestServices.GetService(typeof(IAuthenticationService));
                        if (authService is not null)
                        {
                            var props = new AuthenticationProperties
                            {
                                IsPersistent = true,
                                AllowRefresh = true,
                                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                            };
                            await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, HttpContext.User!, props);
                        }
                    }
                }
                else
                {
                    // As a last resort, attempt to re-issue cookie using the configured cookie auth scheme
                    var authService = HttpContext.RequestServices.GetService(typeof(IAuthenticationService));
                    if (authService is not null)
                    {
                        var props = new AuthenticationProperties
                        {
                            IsPersistent = true,
                            AllowRefresh = true,
                            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                        };
                        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, HttpContext.User!, props);
                    }
                }
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh session on keepalive.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { code = "KEEPALIVE_FAILED", message = "Unable to refresh session." });
        }
    }
}
