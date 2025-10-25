using System.Text.Json;
using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Provides example data for paginated TurnDto responses.
/// </summary>
public class PagedTurnDtoExample : IExamplesProvider<PagedResult<TurnDto>>
{
    public PagedResult<TurnDto> GetExamples()
    {
        return new PagedResult<TurnDto>
        {
            Items = new List<TurnDto>
            {
                new TurnDto(
                    Id: 42,
                    TurnNo: 5,
                    ParticipantId: 1,
                    CommittedAt: DateTimeOffset.Parse("2025-10-20T11:30:00Z"),
                    DurationMs: 12500,
                    Summary: JsonDocument.Parse(@"{""actions"": 3, ""unitsMovedCount"": 2, ""citiesFoundedCount"": 0}")),
                new TurnDto(
                    Id: 41,
                    TurnNo: 4,
                    ParticipantId: 2,
                    CommittedAt: DateTimeOffset.Parse("2025-10-20T11:20:00Z"),
                    DurationMs: 8200,
                    Summary: JsonDocument.Parse(@"{""actions"": 2, ""unitsMovedCount"": 1, ""citiesFoundedCount"": 1}")),
                new TurnDto(
                    Id: 40,
                    TurnNo: 3,
                    ParticipantId: 1,
                    CommittedAt: DateTimeOffset.Parse("2025-10-20T11:10:00Z"),
                    DurationMs: 15300,
                    Summary: JsonDocument.Parse(@"{""actions"": 4, ""unitsMovedCount"": 3, ""citiesFoundedCount"": 0}")),
                new TurnDto(
                    Id: 39,
                    TurnNo: 2,
                    ParticipantId: 2,
                    CommittedAt: DateTimeOffset.Parse("2025-10-20T11:00:00Z"),
                    DurationMs: 6800,
                    Summary: JsonDocument.Parse(@"{""actions"": 1, ""unitsMovedCount"": 1, ""citiesFoundedCount"": 0}")),
                new TurnDto(
                    Id: 38,
                    TurnNo: 1,
                    ParticipantId: 1,
                    CommittedAt: DateTimeOffset.Parse("2025-10-20T10:50:00Z"),
                    DurationMs: 9500,
                    Summary: JsonDocument.Parse(@"{""actions"": 2, ""unitsMovedCount"": 2, ""citiesFoundedCount"": 0}"))
            },
            Page = 1,
            PageSize = 20,
            Total = 5
        };
    }
}

