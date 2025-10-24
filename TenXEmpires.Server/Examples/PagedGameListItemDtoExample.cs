using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Provides example data for paginated GameListItemDto responses.
/// </summary>
public class PagedGameListItemDtoExample : IExamplesProvider<PagedResult<GameListItemDto>>
{
    public PagedResult<GameListItemDto> GetExamples()
    {
        return new PagedResult<GameListItemDto>
        {
            Items = new List<GameListItemDto>
            {
                new GameListItemDto(
                    Id: 1,
                    Status: "active",
                    TurnNo: 5,
                    MapId: 1,
                    MapSchemaVersion: 1,
                    StartedAt: DateTimeOffset.Parse("2025-10-20T10:00:00Z"),
                    FinishedAt: null,
                    LastTurnAt: DateTimeOffset.Parse("2025-10-20T11:30:00Z")),
                new GameListItemDto(
                    Id: 2,
                    Status: "finished",
                    TurnNo: 10,
                    MapId: 1,
                    MapSchemaVersion: 1,
                    StartedAt: DateTimeOffset.Parse("2025-10-15T14:00:00Z"),
                    FinishedAt: DateTimeOffset.Parse("2025-10-15T15:45:00Z"),
                    LastTurnAt: DateTimeOffset.Parse("2025-10-15T15:45:00Z")),
                new GameListItemDto(
                    Id: 3,
                    Status: "active",
                    TurnNo: 15,
                    MapId: 2,
                    MapSchemaVersion: 1,
                    StartedAt: DateTimeOffset.Parse("2025-10-22T09:00:00Z"),
                    FinishedAt: null,
                    LastTurnAt: DateTimeOffset.Parse("2025-10-23T14:20:00Z"))
            },
            Page = 1,
            PageSize = 20,
            Total = 15
        };
    }
}

