using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Services;

/// <summary>
/// Service implementation for game-related business logic and queries.
/// </summary>
public class GameService : IGameService
{
    private readonly TenXDbContext _context;
    private readonly ILogger<GameService> _logger;

    public GameService(TenXDbContext context, ILogger<GameService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<GameListItemDto>> ListGamesAsync(
        Guid userId,
        ListGamesQuery query,
        CancellationToken cancellationToken = default)
    {
        // Start with base query filtered by user (defense-in-depth with RLS)
        var baseQuery = _context.Games
            .AsNoTracking()
            .Where(g => g.UserId == userId);

        // Apply status filter if provided
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var normalizedStatus = query.Status.ToLowerInvariant();
            if (GameStatus.ValidStatuses.Contains(normalizedStatus))
            {
                baseQuery = baseQuery.Where(g => g.Status == normalizedStatus);
            }
            else
            {
                throw new ArgumentException(
                    $"Invalid status '{query.Status}'. Must be one of: {string.Join(", ", GameStatus.ValidStatuses)}.",
                    nameof(query.Status));
            }
        }

        // Apply sorting
        var sortField = (query.Sort ?? GameSortField.Default).ToLowerInvariant();
        var sortOrder = (query.Order ?? SortOrder.Default).ToLowerInvariant();

        if (!SortOrder.ValidOrders.Contains(sortOrder))
        {
            throw new ArgumentException(
                $"Invalid order '{query.Order}'. Must be one of: {string.Join(", ", SortOrder.ValidOrders)}.",
                nameof(query.Order));
        }

        if (!GameSortField.ValidFields.Contains(sortField))
        {
            throw new ArgumentException(
                $"Invalid sort field '{query.Sort}'. Must be one of: {string.Join(", ", GameSortField.ValidFields)}.",
                nameof(query.Sort));
        }

        var isAscending = sortOrder == SortOrder.Ascending;

        baseQuery = sortField switch
        {
            GameSortField.StartedAt => isAscending
                ? baseQuery.OrderBy(g => g.StartedAt)
                : baseQuery.OrderByDescending(g => g.StartedAt),
            GameSortField.LastTurnAt => isAscending
                ? baseQuery.OrderBy(g => g.LastTurnAt ?? g.StartedAt)
                : baseQuery.OrderByDescending(g => g.LastTurnAt ?? g.StartedAt),
            GameSortField.TurnNo => isAscending
                ? baseQuery.OrderBy(g => g.TurnNo)
                : baseQuery.OrderByDescending(g => g.TurnNo),
            _ => throw new ArgumentException(
                $"Invalid sort field '{query.Sort}'. Must be one of: {string.Join(", ", GameSortField.ValidFields)}.",
                nameof(query.Sort))
        };

        // Get total count (optional for performance - could be deferred)
        var total = await baseQuery.CountAsync(cancellationToken);

        // Apply pagination
        var skip = (query.Page - 1) * query.PageSize;
        var items = await baseQuery
            .Skip(skip)
            .Take(query.PageSize)
            .Select(g => new GameListItemDto(
                g.Id,
                g.Status,
                g.TurnNo,
                g.MapId,
                g.MapSchemaVersion,
                g.StartedAt,
                g.FinishedAt,
                g.LastTurnAt))
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Listed {Count} games for user {UserId} (page {Page}, status: {Status}, sort: {Sort} {Order})",
            items.Count,
            userId,
            query.Page,
            query.Status ?? "all",
            sortField,
            sortOrder);

        return new PagedResult<GameListItemDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total
        };
    }
}

