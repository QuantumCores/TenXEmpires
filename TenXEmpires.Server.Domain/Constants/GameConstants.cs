namespace TenXEmpires.Server.Domain.Constants;

/// <summary>
/// Constants for game status values.
/// </summary>
public static class GameStatus
{
    public const string Active = "active";
    public const string Finished = "finished";
    
    /// <summary>
    /// Gets all valid game status values.
    /// </summary>
    public static readonly string[] ValidStatuses = { Active, Finished };
}

/// <summary>
/// Constants for game query sort fields.
/// </summary>
public static class GameSortField
{
    public const string StartedAt = "startedat";
    public const string LastTurnAt = "lastturnat";
    public const string TurnNo = "turnno";
    
    /// <summary>
    /// Gets all valid sort field values (normalized to lowercase).
    /// </summary>
    public static readonly string[] ValidFields = { StartedAt, LastTurnAt, TurnNo };
    
    /// <summary>
    /// The default sort field for game queries.
    /// </summary>
    public const string Default = LastTurnAt;
}

/// <summary>
/// Constants for sort order direction.
/// </summary>
public static class SortOrder
{
    public const string Ascending = "asc";
    public const string Descending = "desc";
    
    /// <summary>
    /// Gets all valid sort order values.
    /// </summary>
    public static readonly string[] ValidOrders = { Ascending, Descending };
    
    /// <summary>
    /// The default sort order for queries.
    /// </summary>
    public const string Default = Descending;
}

/// <summary>
/// Constants for participant types in a game.
/// </summary>
public static class ParticipantKind
{
    public const string Human = "human";
    public const string Ai = "ai";
    
    /// <summary>
    /// Gets all valid participant kind values.
    /// </summary>
    public static readonly string[] ValidKinds = { Human, Ai };
}

/// <summary>
/// Custom header names used by TenX Empires API.
/// </summary>
public static class TenxHeaders
{
    /// <summary>
    /// Header for idempotency key to prevent duplicate operations.
    /// </summary>
    public const string IdempotencyKey = "X-Tenx-Idempotency-Key";
    
    /// <summary>
    /// Header for correlation ID for request tracing.
    /// </summary>
    public const string CorrelationId = "X-Tenx-Correlation-Id";
}

