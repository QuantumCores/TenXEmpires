namespace TenXEmpires.Server.Domain.Constants;

/// <summary>
/// Allowed analytics event types and helpers.
/// </summary>
public static class AnalyticsEventTypes
{
    public const string GameStart = "game_start";
    public const string TurnEnd = "turn_end";
    public const string Autosave = "autosave";
    public const string ManualSave = "manual_save";
    public const string ManualLoad = "manual_load";
    public const string GameFinish = "game_finish";

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        GameStart, TurnEnd, Autosave, ManualSave, ManualLoad, GameFinish
    };

    /// <summary>
    /// Returns true if the event type is allowed or explicitly whitelisted by prefix for custom events.
    /// Custom, forward-compatible events should use the "custom." prefix.
    /// </summary>
    public static bool IsValid(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return false;
        }

        if (Allowed.Contains(eventType))
        {
            return true;
        }

        // Allow forward-compatible custom events under a namespaced prefix
        return eventType.StartsWith("custom.", StringComparison.OrdinalIgnoreCase);
    }
}

