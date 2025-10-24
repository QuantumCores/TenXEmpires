using System.Security.Claims;

namespace TenXEmpires.Server.Extensions;

/// <summary>
/// Extension methods for ClaimsPrincipal to simplify user identity extraction.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extracts the authenticated user's ID from claims.
    /// </summary>
    /// <param name="user">The claims principal representing the user.</param>
    /// <returns>The user's ID as a Guid.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user ID cannot be extracted or parsed.</exception>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim))
        {
            throw new UnauthorizedAccessException("User ID claim not found. User must be authenticated.");
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID claim is not a valid GUID.");
        }

        return userId;
    }

    /// <summary>
    /// Attempts to extract the authenticated user's ID from claims.
    /// </summary>
    /// <param name="user">The claims principal representing the user.</param>
    /// <param name="userId">The extracted user ID if successful.</param>
    /// <returns>True if the user ID was successfully extracted, false otherwise.</returns>
    public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId)
    {
        userId = Guid.Empty;
        
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return false;
        }

        return Guid.TryParse(userIdClaim, out userId);
    }
}

