using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Swagger example for GameDetailDto (GET /games/{id} response).
/// </summary>
public class GameDetailDtoExample : IExamplesProvider<GameDetailDto>
{
    public GameDetailDto GetExamples()
    {
        // Example with representative values
        var settingsJson = System.Text.Json.JsonDocument.Parse("""
        {
          "difficulty": "normal",
          "map": { "code": "standard_15x20", "schemaVersion": 1 }
        }
        """);

        return new GameDetailDto(
            Id: 42,
            UserId: Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            MapId: 1,
            MapSchemaVersion: 1,
            TurnNo: 5,
            ActiveParticipantId: 101,
            TurnInProgress: false,
            Status: "active",
            StartedAt: DateTimeOffset.Parse("2025-10-20T10:00:00Z"),
            FinishedAt: null,
            LastTurnAt: DateTimeOffset.Parse("2025-10-20T11:30:00Z"),
            Settings: settingsJson);
    }
}

