namespace TenXEmpires.Server.Domain.Entities.App;

/// <summary>
/// Represents a participant in a game (human player or AI)
/// </summary>
public class Participant
{
    public long Id { get; set; }
    
    public long GameId { get; set; }
    
    /// <summary>
    /// Type of participant: 'human' or 'ai'
    /// </summary>
    public string Kind { get; set; } = string.Empty;
    
    /// <summary>
    /// User ID for human participants, null for AI
    /// </summary>
    public Guid? UserId { get; set; }
    
    public string DisplayName { get; set; } = string.Empty;
    
    public bool IsEliminated { get; set; }
    
    // Navigation properties
    public Game Game { get; set; } = null!;
    
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
    
    public ICollection<City> Cities { get; set; } = new List<City>();
    
    public ICollection<Turn> Turns { get; set; } = new List<Turn>();
}

