namespace TenXEmpires.Server.Domain.Entities;

/// <summary>
/// Represents a game map with its dimensions and metadata
/// </summary>
public class Map
{
    public long Id { get; set; }
    
    public string Code { get; set; } = string.Empty;
    
    public int SchemaVersion { get; set; }
    
    public int Width { get; set; }
    
    public int Height { get; set; }
    
    // Navigation properties
    public ICollection<MapTile> MapTiles { get; set; } = new List<MapTile>();
    
    public ICollection<Game> Games { get; set; } = new List<Game>();
}

