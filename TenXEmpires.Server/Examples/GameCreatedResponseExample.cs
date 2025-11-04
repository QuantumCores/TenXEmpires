using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Swagger example for GameCreatedResponse (POST /games response).
/// </summary>
public class GameCreatedResponseExample : IExamplesProvider<GameCreatedResponse>
{
    public GameCreatedResponse GetExamples()
    {
        // Create sample game state game
        var game = new GameStateGameDto(
            Id: 42,
            TurnNo: 1,
            ActiveParticipantId: 101,
            TurnInProgress: false,
            Status: "active");

        // Create sample map
        var map = new GameStateMapDto(
            Id: 1,
            Code: "standard_15x20",
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

        // Create sample units
        var units = new List<UnitInStateDto>
        {
            new UnitInStateDto(
                Id: 201,
                ParticipantId: 101,
                TypeCode: "warrior",
                Hp: 100,
                HasActed: false,
                TileId: 5,
                Row: 0,
                Col: 1),
            new UnitInStateDto(
                Id: 202,
                ParticipantId: 102,
                TypeCode: "warrior",
                Hp: 100,
                HasActed: false,
                TileId: 43,
                Row: 5,
                Col: 7)
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
            new CityResourceDto(CityId: 301, ResourceType: "food", Amount: 5),
            new CityResourceDto(CityId: 301, ResourceType: "production", Amount: 2),
            new CityResourceDto(CityId: 302, ResourceType: "food", Amount: 5),
            new CityResourceDto(CityId: 302, ResourceType: "production", Amount: 2)
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
                Health: 50)
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
        return new GameCreatedResponse(42, gameState);
    }
}

