namespace TenXEmpires.Server.Domain.Constants;

/// <summary>
/// Constants for terrain types on map tiles.
/// </summary>
public static class TerrainTypes
{
    public const string Grassland = "grassland";
    public const string Water = "water";
    public const string Ocean = "ocean";
    public const string Tundra = "tundra";
    public const string Tropical = "tropical";

    /// <summary>
    /// Gets all valid terrain types.
    /// </summary>
    public static readonly string[] ValidTypes = { Grassland, Water, Ocean, Tundra, Tropical };

    /// <summary>
    /// Gets terrain types that are considered water (not suitable for city placement).
    /// </summary>
    public static readonly string[] WaterTerrains = { Water, Ocean };

    /// <summary>
    /// Checks if a terrain type is water.
    /// </summary>
    public static bool IsWater(string terrain) => 
        terrain == Water || terrain == Ocean;
}

