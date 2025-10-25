using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Service for turn-related business logic and queries.
/// </summary>
public interface ITurnService
{
    /// <summary>
    /// Lists turns for a specific game with sorting and pagination.
    /// </summary>
    /// <param name="gameId">The game ID to list turns for.</param>
    /// <param name="query">Query parameters for sorting and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paged result containing turn DTOs.</returns>
    /// <exception cref="ArgumentException">Thrown when sort or order parameters are invalid.</exception>
    Task<PagedResult<TurnDto>> ListTurnsAsync(
        long gameId,
        ListTurnsQuery query,
        CancellationToken cancellationToken = default);
}

