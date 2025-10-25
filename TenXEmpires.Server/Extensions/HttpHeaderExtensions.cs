using TenXEmpires.Server.Domain.Constants;

namespace TenXEmpires.Server.Extensions;

/// <summary>
/// HTTP header convenience helpers for conditional caching and standard header usage.
/// </summary>
public static class HttpHeaderExtensions
{
    /// <summary>
    /// Returns true if the request's If-None-Match header matches the provided ETag.
    /// Supports multiple comma-separated values and wildcard '*'.
    /// Treats weak validators (W/"...") as matching their strong value.
    /// </summary>
    public static bool IsNotModified(this HttpRequest request, string currentEtag)
    {
        var ifNoneMatch = request.Headers[StandardHeaders.IfNoneMatch].ToString();
        if (string.IsNullOrWhiteSpace(ifNoneMatch))
        {
            return false;
        }

        var values = ifNoneMatch
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.Trim())
            .ToArray();

        if (values.Length == 0)
        {
            return false;
        }

        if (values.Any(v => v == "*"))
        {
            return true;
        }

        // Normalize possible weak validators
        static string Normalize(string v) => v.StartsWith("W/\"") ? v[2..] : v;

        var normalizedClient = values.Select(Normalize);
        var normalizedCurrent = Normalize(currentEtag);

        return normalizedClient.Contains(normalizedCurrent, StringComparer.Ordinal);
    }

    /// <summary>
    /// Sets the ETag header.
    /// </summary>
    public static void SetETag(this HttpResponse response, string etag)
    {
        response.Headers[StandardHeaders.ETag] = etag;
    }

    /// <summary>
    /// Composes a strong, quoted ETag for a game using id, turn and timestamp.
    /// </summary>
    public static string ComposeGameETag(long gameId, int turnNo, DateTimeOffset? lastTurnAt)
    {
        var ts = lastTurnAt?.ToUnixTimeSeconds() ?? 0;
        return $"\"g:{gameId}:t:{turnNo}:ts:{ts}\"";
    }
}
