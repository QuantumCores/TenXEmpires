namespace TenXEmpires.Server.Domain.Constants;

/// <summary>
/// Constants for unit type codes.
/// </summary>
public static class UnitTypes
{
    public const string Warrior = "warrior";
    public const string Slinger = "slinger";

    /// <summary>
    /// Gets all valid unit type codes for MVP.
    /// </summary>
    public static readonly string[] ValidTypes = { Warrior, Slinger };
}

