using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Service for game-related business logic and queries.
/// </summary>
public interface IGameService
{
    /// <summary>
    /// Lists games for the authenticated user with filtering, sorting, and pagination.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="query">Query parameters for filtering, sorting, and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paged result containing game list items.</returns>
    Task<PagedResult<GameListItemDto>> ListGamesAsync(
        Guid userId,
        ListGamesQuery query,
        CancellationToken cancellationToken = default);
}

