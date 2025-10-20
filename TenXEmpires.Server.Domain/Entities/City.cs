namespace TenXEmpires.Server.Domain.Entities;

/// <summary>
/// Represents a city in a game
/// </summary>
public class City
{
    public long Id { get; set; }
    
    public long GameId { get; set; }
    
    public long ParticipantId { get; set; }
    
    public long TileId { get; set; }
    
    /// <summary>
    /// Current health points
    /// </summary>
    public int Hp { get; set; }
    
    /// <summary>
    /// Maximum health points
    /// </summary>
    public int MaxHp { get; set; }
    
    // Navigation properties
    public Game Game { get; set; } = null!;
    
    public Participant Participant { get; set; } = null!;
    
    public MapTile Tile { get; set; } = null!;
    
    public ICollection<CityTile> CityTiles { get; set; } = new List<CityTile>();
    
    public ICollection<CityResource> CityResources { get; set; } = new List<CityResource>();
}

