namespace TenXEmpires.Server.Domain.Configuration;

/// <summary>
/// Configuration settings for game creation and management.
/// </summary>
public class GameSettings
{
    /// <summary>
    /// Maximum number of active games a user can have simultaneously.
    /// </summary>
    public int MaxActiveGamesPerUser { get; set; } = 10;

    /// <summary>
    /// The map schema version accepted by the current game engine.
    /// </summary>
    public int AcceptedMapSchemaVersion { get; set; } = 1;

    /// <summary>
    /// The default map code to use when none is specified.
    /// </summary>
    public string DefaultMapCode { get; set; } = "standard_6x8";

    /// <summary>
    /// City healing per turn when not under siege.
    /// </summary>
    public int CityRegenNormal { get; set; } = 4;

    /// <summary>
    /// City healing per turn when under siege.
    /// </summary>
    public int CityRegenUnderSiege { get; set; } = 2;
}

