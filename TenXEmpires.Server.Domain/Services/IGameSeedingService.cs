namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Service for seeding initial game entities (cities, units, resources).
/// </summary>
public interface IGameSeedingService
{
    /// <summary>
    /// Seeds initial cities, units, and resources for a new game.
    /// </summary>
    /// <param name="gameId">The game ID.</param>
    /// <param name="mapId">The map ID.</param>
    /// <param name="humanParticipantId">The human participant ID.</param>
    /// <param name="aiParticipantId">The AI participant ID.</param>
    /// <param name="rngSeed">The RNG seed for deterministic placement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SeedGameEntitiesAsync(
        long gameId,
        long mapId,
        long humanParticipantId,
        long aiParticipantId,
        long rngSeed,
        CancellationToken cancellationToken = default);
}

