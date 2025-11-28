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
    public string DefaultMapCode { get; set; } = "standard_15x20";

    /// <summary>
    /// City healing per turn when not under siege.
    /// </summary>
    public int CityRegenNormal { get; set; } = 4;

    /// <summary>
    /// City healing per turn when under siege.
    /// </summary>
    public int CityRegenUnderSiege { get; set; } = 2;

    /// <summary>
    /// Maximum resource storage per type per city.
    /// </summary>
    public int ResourceStorageCap { get; set; } = 100;

    /// <summary>
    /// Base cost in wheat for territory expansion.
    /// </summary>
    public int TerritoryExpansionBaseCost { get; set; } = 20;

    /// <summary>
    /// Additional wheat cost per extra tile beyond initial territory.
    /// </summary>
    public int TerritoryExpansionCostPerTile { get; set; } = 10;

    /// <summary>
    /// Number of tiles a city starts with (center + 6 neighbors).
    /// </summary>
    public int InitialCityTerritorySize { get; set; } = 7;
}

