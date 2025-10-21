using System.Collections.Generic;

namespace TenXEmpires.Server.Domain.DataContracts;

/// <summary>
/// Generic wrapper for list responses: { items: [...] }
/// </summary>
public sealed record ItemsResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
}

/// <summary>
/// Generic wrapper for paged responses: { items, page, pageSize, total? }
/// </summary>
public sealed record PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int? Total { get; init; }
}

