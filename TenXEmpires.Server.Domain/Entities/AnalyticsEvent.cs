namespace TenXEmpires.Server.Domain.Entities;

/// <summary>
/// Represents an analytics event (outside RLS, retained after account deletion)
/// </summary>
public class AnalyticsEvent
{
    public long Id { get; set; }
    
    public string EventType { get; set; } = string.Empty;
    
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Copy of game ID (no FK to allow retention after deletion)
    /// </summary>
    public long GameKey { get; set; }
    
    /// <summary>
    /// Salted hash of user_id (64 char hex string)
    /// </summary>
    public string UserKey { get; set; } = string.Empty;
    
    public int SaltVersion { get; set; }
    
    public int? TurnNo { get; set; }
    
    public string? MapCode { get; set; }
    
    public int? MapSchemaVersion { get; set; }
    
    public long? RngSeed { get; set; }
    
    public DateTimeOffset? GameStartedAt { get; set; }
    
    public DateTimeOffset? GameFinishedAt { get; set; }
    
    public int? ParticipantCount { get; set; }
    
    /// <summary>
    /// Client request ID for idempotency
    /// </summary>
    public Guid? ClientRequestId { get; set; }
}

