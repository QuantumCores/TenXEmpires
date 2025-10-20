namespace TenXEmpires.Server.Domain.Entities.App;

/// <summary>
/// Defines the static properties and stats for a unit type
/// </summary>
public class UnitDefinition
{
    public long Id { get; set; }
    
    /// <summary>
    /// Unit type code (e.g., 'warrior', 'slinger', 'archer')
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    public bool IsRanged { get; set; }
    
    public int Attack { get; set; }
    
    public int Defence { get; set; }
    
    public int RangeMin { get; set; }
    
    public int RangeMax { get; set; }
    
    public int MovePoints { get; set; }
    
    /// <summary>
    /// Maximum health points for this unit type
    /// </summary>
    public int Health { get; set; }
    
    // Navigation properties
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}

