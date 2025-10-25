using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Services;

/// <summary>
/// Service implementation for turn-related business logic and queries.
/// </summary>
public class TurnService : ITurnService
{
    private readonly TenXDbContext _context;
    private readonly ILogger<TurnService> _logger;

    public TurnService(
        TenXDbContext context,
        ILogger<TurnService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<TurnDto>> ListTurnsAsync(
        long gameId,
        ListTurnsQuery query,
        CancellationToken cancellationToken = default)
    {
        // Start with base query filtered by game ID (RLS also applies)
        var baseQuery = _context.Turns
            .AsNoTracking()
            .Where(t => t.GameId == gameId);

        // Apply sorting
        var sortField = (query.Sort ?? TurnSortField.Default).ToLowerInvariant();
        var sortOrder = (query.Order ?? SortOrder.Default).ToLowerInvariant();

        if (!SortOrder.ValidOrders.Contains(sortOrder))
        {
            throw new ArgumentException(
                $"Invalid order '{query.Order}'. Must be one of: {string.Join(", ", SortOrder.ValidOrders)}.",
                nameof(query.Order));
        }

        if (!TurnSortField.ValidFields.Contains(sortField))
        {
            throw new ArgumentException(
                $"Invalid sort field '{query.Sort}'. Must be one of: {string.Join(", ", TurnSortField.ValidFields)}.",
                nameof(query.Sort));
        }

        var isAscending = sortOrder == SortOrder.Ascending;

        baseQuery = sortField switch
        {
            TurnSortField.TurnNo => isAscending
                ? baseQuery.OrderBy(t => t.TurnNo)
                : baseQuery.OrderByDescending(t => t.TurnNo),
            TurnSortField.CommittedAt => isAscending
                ? baseQuery.OrderBy(t => t.CommittedAt)
                : baseQuery.OrderByDescending(t => t.CommittedAt),
            _ => throw new ArgumentException(
                $"Invalid sort field '{query.Sort}'. Must be one of: {string.Join(", ", TurnSortField.ValidFields)}.",
                nameof(query.Sort))
        };

        // Get total count (optional for performance - could be omitted for large datasets)
        var total = await baseQuery.CountAsync(cancellationToken);

        // Apply pagination and fetch turns
        var skip = (query.Page - 1) * query.PageSize;
        var turns = await baseQuery
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // Parse JSON summaries in memory (avoids second database query)
        var items = turns.Select(TurnDto.From).ToList();

        _logger.LogDebug(
            "Listed {Count} turns for game {GameId} (page {Page}, sort: {Sort} {Order})",
            items.Count,
            gameId,
            query.Page,
            sortField,
            sortOrder);

        return new PagedResult<TurnDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total
        };
    }
}

