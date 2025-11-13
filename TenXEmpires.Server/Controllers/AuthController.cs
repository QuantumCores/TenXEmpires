using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

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
    private readonly ILogger<AuthController> _logger;
    private readonly SignInManager<IdentityUser<Guid>> _signInManager;
    private readonly UserManager<IdentityUser<Guid>> _userManager;

    [ActivatorUtilitiesConstructor]
    public AuthController(
        ILogger<AuthController> logger,
        SignInManager<IdentityUser<Guid>> signInManager,
        UserManager<IdentityUser<Guid>> userManager)
    {
        _logger = logger;
        _signInManager = signInManager;
        _userManager = userManager;
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
            Response.Headers[TenXEmpires.Server.Domain.Constants.StandardHeaders.CacheControl] = "no-store";

            // If user is authenticated, re-issue auth cookie to extend sliding expiration.
            if (HttpContext.User?.Identity?.IsAuthenticated == true)
            {
                // Only attempt sign-in refresh if an auth service is available in the host
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

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh session on keepalive.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { code = "KEEPALIVE_FAILED", message = "Unable to refresh session." });
        }
    }

    /// <summary>
    /// Returns information about the current authenticated user.
    /// </summary>
    /// <response code="200">Returns the current user info.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status401Unauthorized)]
    [SwaggerResponseExample(StatusCodes.Status401Unauthorized, typeof(TenXEmpires.Server.Examples.ApiErrorUnauthorizedExample))]
    public async Task<IActionResult> Me()
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new ApiErrorDto("UNAUTHORIZED", "User must be authenticated."));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized(new ApiErrorDto("UNAUTHORIZED", "User not found."));
        }

        return Ok(new
        {
            id = user.Id,
            email = await _userManager.GetEmailAsync(user)
        });
    }

    /// <summary>
    /// Registers a new account and signs the user in.
    /// </summary>
    /// <response code="204">Registered and signed in.</response>
    /// <response code="400">Invalid input or user already exists.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(TenXEmpires.Server.Examples.ApiErrorInvalidInputExample))]
    public async Task<IActionResult> Register([FromBody] TenXEmpires.Server.Domain.DataContracts.RegisterRequestDto body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
        {
            return BadRequest(new ApiErrorDto("INVALID_INPUT", "Email and password are required."));
        }

        var existing = await _userManager.FindByEmailAsync(body.Email);
        if (existing is not null)
        {
            return BadRequest(new ApiErrorDto("USER_EXISTS", "An account with this email already exists."));
        }

        var user = new IdentityUser<Guid>
        {
            Id = Guid.NewGuid(),
            UserName = body.Email,
            Email = body.Email,
            EmailConfirmed = false,
        };

        var createResult = await _userManager.CreateAsync(user, body.Password);
        if (!createResult.Succeeded)
        {
            var message = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return BadRequest(new ApiErrorDto("REGISTRATION_FAILED", message));
        }

        await _signInManager.SignInAsync(user, isPersistent: true);
        return NoContent();
    }

    /// <summary>
    /// Authenticates a user with email and password.
    /// </summary>
    /// <response code="204">Signed in.</response>
    /// <response code="400">Invalid credentials.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(TenXEmpires.Server.Examples.ApiErrorInvalidInputExample))]
    public async Task<IActionResult> Login([FromBody] TenXEmpires.Server.Domain.DataContracts.LoginRequestDto body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
        {
            return BadRequest(new ApiErrorDto("INVALID_INPUT", "Email and password are required."));
        }

        // PasswordSignInAsync requires a user name; we use email as username
        var user = await _userManager.FindByEmailAsync(body.Email);
        if (user is null)
        {
            return BadRequest(new ApiErrorDto("INVALID_CREDENTIALS", "Invalid email or password."));
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, body.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            return BadRequest(new ApiErrorDto("INVALID_CREDENTIALS", "Invalid email or password."));
        }

        await _signInManager.SignInAsync(user, isPersistent: body.RememberMe);
        return NoContent();
    }

    /// <summary>
    /// Signs the current user out.
    /// </summary>
    /// <response code="204">Signed out.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status401Unauthorized)]
    [SwaggerResponseExample(StatusCodes.Status401Unauthorized, typeof(TenXEmpires.Server.Examples.ApiErrorUnauthorizedExample))]
    public async Task<IActionResult> Logout()
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new ApiErrorDto("UNAUTHORIZED", "User must be authenticated."));
        }
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    /// <summary>
    /// Requests a password reset email for the specified account.
    /// </summary>
    /// <remarks>
    /// Returns a generic success response regardless of whether the email exists to prevent account enumeration.
    /// If the email is associated with an account, a password reset link will be sent.
    /// </remarks>
    /// <response code="204">Request processed (email sent if account exists).</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="429">Too many requests (rate limited).</response>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(TenXEmpires.Server.Examples.ApiErrorInvalidInputExample))]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status429TooManyRequests)]
    [SwaggerResponseExample(StatusCodes.Status429TooManyRequests, typeof(TenXEmpires.Server.Examples.ApiErrorRateLimitExample))]
    public async Task<IActionResult> ForgotPassword([FromBody] TenXEmpires.Server.Domain.DataContracts.ForgotPasswordRequestDto body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Email))
        {
            return BadRequest(new ApiErrorDto("INVALID_INPUT", "Email is required."));
        }

        // Find user by email
        var user = await _userManager.FindByEmailAsync(body.Email);
        
        // Always return success to prevent account enumeration
        // If user exists, generate and log the reset token (email sending not implemented yet)
        if (user is not null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            
            // TODO: Send email with reset link containing the token
            // For now, just log it for development purposes
            _logger.LogInformation(
                "Password reset requested for user {UserId}. Token: {Token} (Email sending not implemented)",
                user.Id,
                token
            );
        }
        else
        {
            _logger.LogInformation(
                "Password reset requested for non-existent email: {Email}",
                body.Email
            );
        }

        // Always return 204 regardless of whether user exists
        return NoContent();
    }

    /// <summary>
    /// Resends the email verification link to the user's email address.
    /// </summary>
    /// <remarks>
    /// If authenticated, uses the current user's email. If not authenticated and email is provided,
    /// sends verification email to that address. Returns success regardless of whether account exists
    /// to prevent account enumeration.
    /// </remarks>
    /// <response code="204">Verification email sent (if account exists and is unverified).</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="429">Too many requests (rate limited).</response>
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(TenXEmpires.Server.Examples.ApiErrorInvalidInputExample))]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status429TooManyRequests)]
    [SwaggerResponseExample(StatusCodes.Status429TooManyRequests, typeof(TenXEmpires.Server.Examples.ApiErrorRateLimitExample))]
    public async Task<IActionResult> ResendVerification([FromBody] TenXEmpires.Server.Domain.DataContracts.ResendVerificationRequestDto body)
    {
        string? targetEmail = null;

        // If user is authenticated, use their email
        if (User?.Identity?.IsAuthenticated == true)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is not null)
            {
                targetEmail = await _userManager.GetEmailAsync(currentUser);
            }
        }
        // Otherwise, use the provided email from the request body
        else if (body is not null && !string.IsNullOrWhiteSpace(body.Email))
        {
            targetEmail = body.Email;
        }

        // If no email could be determined, return bad request
        if (string.IsNullOrWhiteSpace(targetEmail))
        {
            return BadRequest(new ApiErrorDto("INVALID_INPUT", "Email is required."));
        }

        // Find user by email
        var user = await _userManager.FindByEmailAsync(targetEmail);

        // Always return success to prevent account enumeration
        // If user exists and email is not confirmed, generate and log the verification token
        if (user is not null && !user.EmailConfirmed)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            
            // TODO: Send email with verification link containing the token
            // For now, just log it for development purposes
            _logger.LogInformation(
                "Email verification requested for user {UserId}. Token: {Token} (Email sending not implemented)",
                user.Id,
                token
            );
        }
        else if (user is not null && user.EmailConfirmed)
        {
            _logger.LogInformation(
                "Verification email requested for already verified user: {UserId}",
                user.Id
            );
        }
        else
        {
            _logger.LogInformation(
                "Verification email requested for non-existent email: {Email}",
                targetEmail
            );
        }

        // Always return 204 regardless of whether user exists or is already verified
        return NoContent();
    }
}
