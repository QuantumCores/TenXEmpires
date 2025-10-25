using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Swagger example for ActionStateResponse (POST /games/{id}/actions/move response).
/// </summary>
public class ActionStateResponseExample : IExamplesProvider<ActionStateResponse>
{
    public ActionStateResponse GetExamples()
    {
        // Create sample game state game
        var game = new GameStateGameDto(
            Id: 42,
            TurnNo: 3,
            ActiveParticipantId: 101,
            TurnInProgress: false,
            Status: "active");

        // Create sample map
        var map = new GameStateMapDto(
            Id: 1,
            Code: "standard_6x8",
            SchemaVersion: 1,
            Width: 8,
            Height: 6);

        // Create sample participants
        var participants = new List<ParticipantDto>
        {
            new ParticipantDto(
                Id: 101,
                GameId: 42,
                Kind: "human",
                UserId: Guid.Parse("a0000000-0000-0000-0000-000000000001"),
                DisplayName: "Commander Alex",
                IsEliminated: false),
            new ParticipantDto(
                Id: 102,
                GameId: 42,
                Kind: "ai",
                UserId: null,
                DisplayName: "Genghis Khan",
                IsEliminated: false)
        };

        // Create sample units - note the moved unit is now at a different position
        var units = new List<UnitInStateDto>
        {
            new UnitInStateDto(
                Id: 201,
                ParticipantId: 101,
                TypeCode: "warrior",
                Hp: 100,
                HasActed: true,  // Unit has acted after moving
                TileId: 13,      // New tile after move
                Row: 2,          // New row position
                Col: 3),         // New col position
            new UnitInStateDto(
                Id: 202,
                ParticipantId: 102,
                TypeCode: "warrior",
                Hp: 100,
                HasActed: false,
                TileId: 43,
                Row: 5,
                Col: 7),
            new UnitInStateDto(
                Id: 203,
                ParticipantId: 101,
                TypeCode: "settler",
                Hp: 50,
                HasActed: false,
                TileId: 7,
                Row: 0,
                Col: 2)
        };

        // Create sample cities
        var cities = new List<CityInStateDto>
        {
            new CityInStateDto(
                Id: 301,
                ParticipantId: 101,
                Hp: 100,
                MaxHp: 100,
                TileId: 3,
                Row: 0,
                Col: 0),
            new CityInStateDto(
                Id: 302,
                ParticipantId: 102,
                Hp: 100,
                MaxHp: 100,
                TileId: 45,
                Row: 5,
                Col: 6)
        };

        // Create sample city tiles
        var cityTiles = new List<CityTileLinkDto>
        {
            new CityTileLinkDto(CityId: 301, TileId: 3),
            new CityTileLinkDto(CityId: 302, TileId: 45)
        };

        // Create sample city resources
        var cityResources = new List<CityResourceDto>
        {
            new CityResourceDto(CityId: 301, ResourceType: "food", Amount: 8),
            new CityResourceDto(CityId: 301, ResourceType: "production", Amount: 4),
            new CityResourceDto(CityId: 302, ResourceType: "food", Amount: 7),
            new CityResourceDto(CityId: 302, ResourceType: "production", Amount: 3)
        };

        // Create sample unit definitions
        var unitDefinitions = new List<UnitDefinitionDto>
        {
            new UnitDefinitionDto(
                Id: 1,
                Code: "warrior",
                IsRanged: false,
                Attack: 20,
                Defence: 10,
                RangeMin: 0,
                RangeMax: 0,
                MovePoints: 2,
                Health: 100),
            new UnitDefinitionDto(
                Id: 2,
                Code: "settler",
                IsRanged: false,
                Attack: 0,
                Defence: 5,
                RangeMin: 0,
                RangeMax: 0,
                MovePoints: 2,
                Health: 50),
            new UnitDefinitionDto(
                Id: 3,
                Code: "archer",
                IsRanged: true,
                Attack: 15,
                Defence: 8,
                RangeMin: 2,
                RangeMax: 2,
                MovePoints: 2,
                Health: 80)
        };

        // Build game state
        var gameState = new GameStateDto(
            game,
            map,
            participants,
            units,
            cities,
            cityTiles,
            cityResources,
            unitDefinitions,
            TurnSummary: null);

        // Return response
        return new ActionStateResponse(gameState);
    }
}

