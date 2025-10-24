namespace TenXEmpires.Server.Domain.Constants;

/// <summary>
/// Constants for resource types used in cities and tiles.
/// </summary>
public static class ResourceTypes
{
    public const string Wood = "wood";
    public const string Stone = "stone";
    public const string Wheat = "wheat";
    public const string Iron = "iron";

    /// <summary>
    /// Gets all valid resource types.
    /// </summary>
    public static readonly string[] ValidTypes = { Wood, Stone, Wheat, Iron };

    /// <summary>
    /// Initial resource amounts when a city is founded.
    /// </summary>
    public static class InitialAmounts
    {
        public const int Wood = 5;
        public const int Stone = 5;
        public const int Wheat = 5;
        public const int Iron = 0;
    }
}

