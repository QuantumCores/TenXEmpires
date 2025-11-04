using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Middleware;

/// <summary>
/// Middleware that sets PostgreSQL session variables for Row-Level Security (RLS).
/// Sets app.user_id to the authenticated user's ID at the start of each request.
/// </summary>
public class RlsContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RlsContextMiddleware> _logger;

    public RlsContextMiddleware(RequestDelegate next, ILogger<RlsContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, TenXDbContext dbContext)
    {
        // Only set RLS context if user is authenticated
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
            {
                try
                {
                    // Open the connection explicitly to ensure it stays open for the entire request
                    // This prevents connection pooling from giving us different connections
                    var connection = dbContext.Database.GetDbConnection();
                    var wasOpen = connection.State == System.Data.ConnectionState.Open;
                    
                    if (!wasOpen)
                    {
                        await connection.OpenAsync();
                    }
                    
                    // Set the session variable for RLS on this specific connection
                    // Note: Direct string interpolation is safe here because userId is validated as a GUID
                    // PostgreSQL doesn't support parameters in SET commands - must use literal value
                    var userIdString = userId.ToString();
                    var sql = $"SET app.user_id = '{userIdString}'";
                    
                    _logger.LogDebug("Setting RLS context: {Sql}", sql);
                    await dbContext.Database.ExecuteSqlRawAsync(sql);
                    
                    _logger.LogDebug("Set RLS context for user {UserId}", userId);
                    
                    try
                    {
                        // Execute the rest of the pipeline with connection kept open
                        await _next(context);
                    }
                    finally
                    {
                        // Reset and close connection
                        if (!wasOpen)
                        {
                            await dbContext.Database.ExecuteSqlRawAsync("RESET app.user_id");
                            _logger.LogDebug("Reset RLS context for user {UserId}", userId);
                            connection.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error setting RLS context for user {UserId}", userId);
                    throw;
                }
            }
            else
            {
                _logger.LogWarning("Unable to parse user ID from claim: {UserIdClaim}", userIdClaim);
                await _next(context);
            }
        }
        else
        {
            // No authenticated user, proceed without RLS context
            await _next(context);
        }
    }
}

/// <summary>
/// Extension methods for registering RLS middleware.
/// </summary>
public static class RlsContextMiddlewareExtensions
{
    /// <summary>
    /// Adds the RLS context middleware to the application pipeline.
    /// Should be called after UseAuthentication() and UseAuthorization().
    /// </summary>
    public static IApplicationBuilder UseRlsContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RlsContextMiddleware>();
    }
}

