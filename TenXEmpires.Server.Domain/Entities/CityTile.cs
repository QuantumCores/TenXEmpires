namespace TenXEmpires.Server.Domain.Entities;

/// <summary>
/// Represents the tiles that belong to a city (including the city tile itself)
/// </summary>
public class CityTile
{
    public long Id { get; set; }
    
    public long GameId { get; set; }
    
    public long CityId { get; set; }
    
    public long TileId { get; set; }
    
    // Navigation properties
    public Game Game { get; set; } = null!;
    
    public City City { get; set; } = null!;
    
    public MapTile Tile { get; set; } = null!;
}

