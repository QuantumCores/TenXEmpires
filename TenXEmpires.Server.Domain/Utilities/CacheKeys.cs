namespace TenXEmpires.Server.Domain.Utilities;

/// <summary>
/// Centralized cache key builder for idempotency and caching operations.
/// Provides consistent key formats across all services.
/// </summary>
public static class CacheKeys
{
    /// <summary>
    /// Gets the idempotency cache key for game creation operations.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="idempotencyKey">The client-provided idempotency key.</param>
    /// <returns>A formatted cache key string.</returns>
    public static string CreateGameIdempotency(Guid userId, string idempotencyKey)
        => $"create-game:{userId}:{idempotencyKey}";

    /// <summary>
    /// Gets the idempotency cache key for game deletion operations.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="idempotencyKey">The client-provided idempotency key.</param>
    /// <returns>A formatted cache key string.</returns>
    public static string DeleteGameIdempotency(Guid userId, string idempotencyKey)
        => $"delete-game:{userId}:{idempotencyKey}";

    /// <summary>
    /// Gets the idempotency cache key for move unit action operations.
    /// </summary>
    /// <param name="gameId">The game ID.</param>
    /// <param name="idempotencyKey">The client-provided idempotency key.</param>
    /// <returns>A formatted cache key string.</returns>
    public static string MoveUnitIdempotency(long gameId, string idempotencyKey)
        => $"move-unit:{gameId}:{idempotencyKey}";

    // Future idempotency keys for other operations can be added here as needed:
    public static string AttackUnitIdempotency(long gameId, string idempotencyKey)
        => $"attack-unit:{gameId}:{idempotencyKey}";

    public static string AttackCityIdempotency(long gameId, string idempotencyKey)
        => $"attack-city:{gameId}:{idempotencyKey}";
    
    /// <summary>
    /// Gets the idempotency cache key for end-turn operations.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="idempotencyKey">The client-provided idempotency key.</param>
    /// <returns>A formatted cache key string.</returns>
    public static string EndTurnIdempotency(Guid userId, long gameId, string idempotencyKey)
        => $"end-turn:{userId}:{gameId}:{idempotencyKey}";
    //
    public static string CreateManualSaveIdempotency(Guid userId, long gameId, string idempotencyKey)
        => $"create-manual-save:{userId}:{gameId}:{idempotencyKey}";
}

