using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Swagger example for LoadSaveResponse (POST /saves/{saveId}/load response).
/// </summary>
public class LoadSaveResponseExample : IExamplesProvider<LoadSaveResponse>
{
    public LoadSaveResponse GetExamples()
    {
        var game = new GameStateGameDto(
            Id: 42,
            TurnNo: 5,
            ActiveParticipantId: 101,
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
            new ParticipantDto(101, 42, "human", Guid.Parse("a0000000-0000-0000-0000-000000000001"), "Commander Alex", false),
            new ParticipantDto(102, 42, "ai", null, "Genghis Khan", false)
        };

        var units = new List<UnitInStateDto>
        {
            new UnitInStateDto(201, 101, "warrior", 80, true, 12, 1, 4),
            new UnitInStateDto(202, 101, "settler", 50, false, 15, 1, 7),
            new UnitInStateDto(203, 102, "warrior", 100, false, 43, 5, 3)
        };

        var cities = new List<CityInStateDto>
        {
            new CityInStateDto(301, 101, 100, 100, 3, 0, 3),
            new CityInStateDto(302, 102, 85, 100, 45, 5, 5)
        };

        var cityTiles = new List<CityTileLinkDto>
        {
            new CityTileLinkDto(301, 3),
            new CityTileLinkDto(301, 4),
            new CityTileLinkDto(301, 11),
            new CityTileLinkDto(302, 45),
            new CityTileLinkDto(302, 44),
            new CityTileLinkDto(302, 37)
        };

        var cityResources = new List<CityResourceDto>
        {
            new CityResourceDto(301, "food", 12),
            new CityResourceDto(301, "production", 8),
            new CityResourceDto(301, "gold", 5),
            new CityResourceDto(302, "food", 8),
            new CityResourceDto(302, "production", 6),
            new CityResourceDto(302, "gold", 3)
        };

        var tileStates = new List<GameTileStateDto>
        {
            new GameTileStateDto(3, "wheat", 70),
            new GameTileStateDto(45, "iron", 40)
        };

        var unitDefinitions = new List<UnitDefinitionDto>
        {
            new UnitDefinitionDto(1, "warrior", false, 20, 10, 0, 0, 2, 100),
            new UnitDefinitionDto(2, "settler", false, 0, 5, 0, 0, 2, 50),
            new UnitDefinitionDto(3, "archer", true, 15, 8, 2, 2, 2, 80)
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

        return new LoadSaveResponse(GameId: 42, State: state);
    }
}

