using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Swagger example for GameSavesListDto response
/// </summary>
public class GameSavesListDtoExample : IExamplesProvider<GameSavesListDto>
{
    public GameSavesListDto GetExamples()
    {
        var manual = new List<SaveManualDto>
        {
            new(
                Id: 101,
                Slot: 1,
                TurnNo: 5,
                CreatedAt: DateTimeOffset.Parse("2025-10-20T11:30:00Z"),
                Name: "Before attacking Rome"
            ),
            new(
                Id: 102,
                Slot: 2,
                TurnNo: 3,
                CreatedAt: DateTimeOffset.Parse("2025-10-20T10:15:00Z"),
                Name: "Opening strategy"
            )
        };

        var autosaves = new List<SaveAutosaveDto>
        {
            new(
                Id: 201,
                TurnNo: 7,
                CreatedAt: DateTimeOffset.Parse("2025-10-20T14:15:00Z")
            ),
            new(
                Id: 202,
                TurnNo: 6,
                CreatedAt: DateTimeOffset.Parse("2025-10-20T13:45:00Z")
            ),
            new(
                Id: 203,
                TurnNo: 5,
                CreatedAt: DateTimeOffset.Parse("2025-10-20T13:20:00Z")
            )
        };

        return new GameSavesListDto(manual, autosaves);
    }
}

