namespace TenXEmpires.Server.Domain.Entities;

/// <summary>
/// Represents a game instance with its state and configuration
/// </summary>
public class Game
{
    public long Id { get; set; }
    
    public Guid UserId { get; set; }
    
    public long MapId { get; set; }
    
    public int MapSchemaVersion { get; set; }
    
    public int TurnNo { get; set; } = 1;
    
    public long? ActiveParticipantId { get; set; }
    
    public bool TurnInProgress { get; set; }
    
    public long RngSeed { get; set; }
    
    public string RngVersion { get; set; } = "v1";
    
    public string Status { get; set; } = "active";
    
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? FinishedAt { get; set; }
    
    public DateTimeOffset? LastTurnAt { get; set; }
    
    public string Settings { get; set; } = "{}"; // JSONB stored as string
    
    // Navigation properties
    public Map Map { get; set; } = null!;
    
    public Participant? ActiveParticipant { get; set; }
    
    public ICollection<Participant> Participants { get; set; } = new List<Participant>();
    
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
    
    public ICollection<City> Cities { get; set; } = new List<City>();
    
    public ICollection<CityTile> CityTiles { get; set; } = new List<CityTile>();
    
    public ICollection<GameTileState> GameTileStates { get; set; } = new List<GameTileState>();
    
    public ICollection<Save> Saves { get; set; } = new List<Save>();
    
    public ICollection<Turn> Turns { get; set; } = new List<Turn>();
}

