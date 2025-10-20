namespace TenXEmpires.Server.Domain.Entities;

/// <summary>
/// Represents a committed turn in the game ledger (append-only)
/// </summary>
public class Turn
{
    public long Id { get; set; }
    
    public long GameId { get; set; }
    
    public int TurnNo { get; set; }
    
    public long ParticipantId { get; set; }
    
    public DateTimeOffset CommittedAt { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Duration in milliseconds to complete this turn
    /// </summary>
    public int? DurationMs { get; set; }
    
    /// <summary>
    /// JSONB summary stored as string
    /// </summary>
    public string? Summary { get; set; }
    
    // Navigation properties
    public Game Game { get; set; } = null!;
    
    public Participant Participant { get; set; } = null!;
}

