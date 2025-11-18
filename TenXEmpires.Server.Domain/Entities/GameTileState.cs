namespace TenXEmpires.Server.Domain.Entities;

/// <summary>
/// Represents the per-game mutable state for a map tile (e.g., remaining resources).
/// </summary>
public class GameTileState
{
    public long Id { get; set; }

    public long GameId { get; set; }

    public long TileId { get; set; }

    public int ResourceAmount { get; set; }

    // Navigation properties
    public Game Game { get; set; } = null!;

    public MapTile Tile { get; set; } = null!;
}
