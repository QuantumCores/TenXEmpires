using System.Text.Json;
using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Swagger example for EndTurnResponse (POST /games/{id}/end-turn response).
/// </summary>
public class EndTurnResponseExample : IExamplesProvider<EndTurnResponse>
{
    public EndTurnResponse GetExamples()
    {
        var game = new GameStateGameDto(
            Id: 42,
            TurnNo: 2,
            ActiveParticipantId: 102,
            TurnInProgress: false,
            Status: "active");

        var map = new GameStateMapDto(
            Id: 1,
            Code: "standard_15x20",
            SchemaVersion: 1,
            Width: 8,
            Height: 6);

        var participants = new List<ParticipantDto>
        {
            new ParticipantDto(101, 42, "human", Guid.Parse("a0000000-0000-0000-0000-000000000001"), "Player", false),
            new ParticipantDto(102, 42, "ai", null, "Genghis Khan", false)
        };

        var units = new List<UnitInStateDto>
        {
            new UnitInStateDto(201, 101, "warrior", 95, true, 5, 0, 1),
            new UnitInStateDto(202, 102, "warrior", 100, false, 43, 5, 7)
        };

        var cities = new List<CityInStateDto>
        {
            new CityInStateDto(301, 101, 100, 100, 3, 0, 0, true),
            new CityInStateDto(302, 102, 100, 100, 45, 5, 6, false)
        };

        var cityTiles = new List<CityTileLinkDto>
        {
            new CityTileLinkDto(301, 3),
            new CityTileLinkDto(302, 45)
        };

        var cityResources = new List<CityResourceDto>
        {
            new CityResourceDto(301, "wood", 7),
            new CityResourceDto(301, "stone", 6),
            new CityResourceDto(301, "wheat", 8),
            new CityResourceDto(301, "iron", 2),
            new CityResourceDto(302, "wood", 6),
            new CityResourceDto(302, "stone", 6),
            new CityResourceDto(302, "wheat", 7),
            new CityResourceDto(302, "iron", 1)
        };

        var tileStates = new List<GameTileStateDto>
        {
            new GameTileStateDto(3, "wheat", 74),
            new GameTileStateDto(45, "iron", 18)
        };

        var unitDefinitions = new List<UnitDefinitionDto>
        {
            new UnitDefinitionDto(1, "warrior", false, 20, 10, 0, 0, 2, 100),
            new UnitDefinitionDto(2, "slinger", true, 15, 8, 2, 3, 2, 60)
        };

        var state = new GameStateDto(
            game,
            map,
            participants,
            units,
            cities,
            cityTiles,
            cityResources,
            tileStates,
            unitDefinitions,
            TurnSummary: null);

        var summaryJson = JsonSerializer.Serialize(new
        {
            regenAppliedCities = 1,
            harvested = new { wood = 2, stone = 1, wheat = 3, iron = 0 },
            producedUnits = new[] { "warrior" },
            productionDelayed = 0,
            aiExecuted = false
        });
        var summaryDoc = JsonDocument.Parse(summaryJson);

        return new EndTurnResponse(state, summaryDoc, AutosaveId: 9001);
    }
}
