using System.Globalization;
using System.Net;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Configuration;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.Services;

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
    private readonly ITransactionalEmailService _emailService;
    private readonly FrontendSettings _frontendSettings;

    private const string AppName = "TenX Empires";

    [ActivatorUtilitiesConstructor]
    public AuthController(
        ILogger<AuthController> logger,
        SignInManager<IdentityUser<Guid>> signInManager,
        UserManager<IdentityUser<Guid>> userManager,
        ITransactionalEmailService emailService,
        IOptions<FrontendSettings> frontendOptions)
    {
        _logger = logger;
        _signInManager = signInManager;
        _userManager = userManager;
        _emailService = emailService;
        _frontendSettings = frontendOptions.Value;
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

        var cancellationToken = HttpContext.RequestAborted;

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

        var verificationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var sent = await TrySendVerificationEmailAsync(user, verificationToken, cancellationToken);
        if (!sent)
        {
            _logger.LogWarning("Registration email failed for user {UserId}. Rolling back account creation.", user.Id);
            await _userManager.DeleteAsync(user);
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto("EMAIL_FAILED", "Unable to send verification email. Please try again."));
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

        // Sign in with explicit authentication properties to ensure cookie is set correctly
        var authProperties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            IsPersistent = body.RememberMe,
            AllowRefresh = true,
            ExpiresUtc = body.RememberMe 
                ? DateTimeOffset.UtcNow.AddDays(30) 
                : DateTimeOffset.UtcNow.AddMinutes(30)
        };
        
        await _signInManager.SignInAsync(user, authProperties);
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
        var cancellationToken = HttpContext.RequestAborted;

        // Find user by email
        var user = await _userManager.FindByEmailAsync(body.Email);
        
        // Always return success to prevent account enumeration
        // If user exists, generate and log the reset token (email sending not implemented yet)
        if (user is null)
        {
            _logger.LogInformation(
                "Password reset requested for non-existent email: {Email}",
                body.Email
            );
            return NoContent();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var sent = await TrySendPasswordResetEmailAsync(user, token, cancellationToken);
        if (!sent)
        {
            _logger.LogWarning("Password reset email failed to dispatch for user {UserId}.", user.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto("EMAIL_FAILED", "Unable to send password reset email. Please try again."));
        }

        // Always return 204 to avoid enumeration when successful
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
        var cancellationToken = HttpContext.RequestAborted;

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
            var sent = await TrySendVerificationEmailAsync(user, token, cancellationToken);
            if (!sent)
            {
                _logger.LogWarning("Verification email resend failed for user {UserId}.", user.Id);
            }
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

    /// <summary>
    /// Confirms a user's email address using the token sent via email.
    /// </summary>
    /// <response code="204">Email confirmed.</response>
    /// <response code="400">Invalid input, token, or email.</response>
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequestDto body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Token))
        {
            return BadRequest(new ApiErrorDto("INVALID_INPUT", "Email and token are required."));
        }

        var user = await _userManager.FindByEmailAsync(body.Email);
        if (user is null)
        {
            _logger.LogWarning("Email confirmation attempted for non-existent email: {Email}", body.Email);
            return BadRequest(new ApiErrorDto("INVALID_TOKEN", "Invalid or expired verification link."));
        }

        if (user.EmailConfirmed)
        {
            return NoContent();
        }

        var confirmResult = await _userManager.ConfirmEmailAsync(user, body.Token);
        if (!confirmResult.Succeeded)
        {
            var message = string.Join("; ", confirmResult.Errors.Select(e => e.Description));
            _logger.LogWarning("Email confirmation failed for user {UserId}. Errors: {Errors}", user.Id, message);
            return BadRequest(new ApiErrorDto("CONFIRMATION_FAILED", message));
        }

        return NoContent();
    }

    /// <summary>
    /// Completes a password reset using the provided token.
    /// </summary>
    /// <response code="204">Password reset successfully.</response>
    /// <response code="400">Invalid input, token, or user.</response>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto body)
    {
        if (body is null ||
            string.IsNullOrWhiteSpace(body.Email) ||
            string.IsNullOrWhiteSpace(body.Token) ||
            string.IsNullOrWhiteSpace(body.Password) ||
            string.IsNullOrWhiteSpace(body.Confirm))
        {
            return BadRequest(new ApiErrorDto("INVALID_INPUT", "Email, token, and password are required."));
        }

        if (!string.Equals(body.Password, body.Confirm, StringComparison.Ordinal))
        {
            return BadRequest(new ApiErrorDto("PASSWORDS_DO_NOT_MATCH", "Passwords do not match."));
        }

        var user = await _userManager.FindByEmailAsync(body.Email);
        if (user is null)
        {
            _logger.LogWarning("Password reset attempted for non-existent email: {Email}", body.Email);
            return BadRequest(new ApiErrorDto("INVALID_TOKEN", "Invalid or expired password reset link."));
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, body.Token, body.Password);
        if (!resetResult.Succeeded)
        {
            var message = string.Join("; ", resetResult.Errors.Select(e => e.Description));
            _logger.LogWarning("Password reset failed for user {UserId}. Errors: {Errors}", user.Id, message);
            return BadRequest(new ApiErrorDto("RESET_FAILED", message));
        }

        return NoContent();
    }

    private Task<bool> TrySendVerificationEmailAsync(IdentityUser<Guid> user, string token, CancellationToken cancellationToken)
    {
        var actionUrl = BuildFrontendLink(_frontendSettings.VerifyEmailPath, ("email", user.Email ?? string.Empty), ("token", token));
        return TrySendTemplateAsync(
            user,
            $"{AppName} - verify your email",
            EmailTemplateNames.VerifyEmail,
            actionUrl,
            "Verify Email",
            cancellationToken);
    }

    private Task<bool> TrySendPasswordResetEmailAsync(IdentityUser<Guid> user, string token, CancellationToken cancellationToken)
    {
        var actionUrl = BuildFrontendLink(_frontendSettings.ResetPasswordPath, ("email", user.Email ?? string.Empty), ("token", token));
        return TrySendTemplateAsync(
            user,
            $"{AppName} - reset your password",
            EmailTemplateNames.PasswordReset,
            actionUrl,
            "Reset Password",
            cancellationToken);
    }

    private async Task<bool> TrySendTemplateAsync(
        IdentityUser<Guid> user,
        string subject,
        string templateName,
        string actionUrl,
        string actionText,
        CancellationToken cancellationToken)
    {
        try
        {
            var supportEmail = string.IsNullOrWhiteSpace(_frontendSettings.SupportEmail)
                ? "support@tenxempires.com"
                : _frontendSettings.SupportEmail;
            var tokens = new Dictionary<string, string?>
            {
                ["UserEmail"] = user.Email,
                ["ActionUrl"] = actionUrl,
                ["ActionText"] = actionText,
                ["AppName"] = AppName,
                ["SupportEmail"] = supportEmail,
                ["Year"] = DateTimeOffset.UtcNow.Year.ToString(CultureInfo.InvariantCulture)
            };

            await _emailService.SendTemplateAsync(
                user.Email!,
                subject,
                templateName,
                tokens,
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {Template} email to user {UserId}.", templateName, user.Id);
            return false;
        }
    }

    private string BuildFrontendLink(string? relativePath, params (string Key, string Value)[] query)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_frontendSettings.BaseUrl)
            ? "http://localhost:5173"
            : _frontendSettings.BaseUrl.TrimEnd('/');

        var normalizedPath = string.IsNullOrWhiteSpace(relativePath)
            ? string.Empty
            : relativePath!.StartsWith("/", StringComparison.Ordinal)
                ? relativePath
                : "/" + relativePath;

        var queryString = string.Join("&", query.Select(pair =>
            $"{WebUtility.UrlEncode(pair.Key)}={WebUtility.UrlEncode(pair.Value)}"));

        return string.IsNullOrEmpty(queryString)
            ? $"{baseUrl}{normalizedPath}"
            : $"{baseUrl}{normalizedPath}?{queryString}";
    }
}
