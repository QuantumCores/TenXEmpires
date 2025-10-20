namespace TenXEmpires.Server.Domain.Entities;

/// <summary>
/// Represents a single tile on a map with terrain and resource information
/// </summary>
public class MapTile
{
    public long Id { get; set; }
    
    public long MapId { get; set; }
    
    public int Row { get; set; }
    
    public int Col { get; set; }
    
    public string Terrain { get; set; } = string.Empty;
    
    public string? ResourceType { get; set; }
    
    public int ResourceAmount { get; set; }
    
    // Navigation properties
    public Map Map { get; set; } = null!;
    
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
    
    public ICollection<City> Cities { get; set; } = new List<City>();
    
    public ICollection<CityTile> CityTiles { get; set; } = new List<CityTile>();
}

