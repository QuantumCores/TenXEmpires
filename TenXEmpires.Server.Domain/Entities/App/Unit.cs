namespace TenXEmpires.Server.Domain.Entities.App;

/// <summary>
/// Represents a unit instance in a game with its current state
/// </summary>
public class Unit
{
    public long Id { get; set; }
    
    public long GameId { get; set; }
    
    public long ParticipantId { get; set; }
    
    public long TypeId { get; set; }
    
    public long TileId { get; set; }
    
    /// <summary>
    /// Current health points
    /// </summary>
    public int Hp { get; set; }
    
    /// <summary>
    /// Whether the unit has taken an action this turn
    /// </summary>
    public bool HasActed { get; set; }
    
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation properties
    public Game Game { get; set; } = null!;
    
    public Participant Participant { get; set; } = null!;
    
    public UnitDefinition Type { get; set; } = null!;
    
    public MapTile Tile { get; set; } = null!;
}

