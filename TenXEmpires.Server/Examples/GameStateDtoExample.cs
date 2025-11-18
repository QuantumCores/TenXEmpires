using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

/// <summary>
/// Swagger example for GameStateDto (GET /games/{id}/state response).
/// </summary>
public class GameStateDtoExample : IExamplesProvider<GameStateDto>
{
    public GameStateDto GetExamples()
    {
        // Create sample game state game
        var game = new GameStateGameDto(
            Id: 42,
            TurnNo: 5,
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

        // Create sample units with various states
        var units = new List<UnitInStateDto>
        {
            new UnitInStateDto(
                Id: 201,
                ParticipantId: 101,
                TypeCode: "warrior",
                Hp: 80,
                HasActed: true,
                TileId: 12,
                Row: 1,
                Col: 4),
            new UnitInStateDto(
                Id: 202,
                ParticipantId: 101,
                TypeCode: "settler",
                Hp: 50,
                HasActed: false,
                TileId: 15,
                Row: 1,
                Col: 7),
            new UnitInStateDto(
                Id: 203,
                ParticipantId: 102,
                TypeCode: "warrior",
                Hp: 100,
                HasActed: false,
                TileId: 43,
                Row: 5,
                Col: 3)
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
                Col: 3),
            new CityInStateDto(
                Id: 302,
                ParticipantId: 102,
                Hp: 85,
                MaxHp: 100,
                TileId: 45,
                Row: 5,
                Col: 5)
        };

        // Create sample city tiles (cities with their controlled territories)
        var cityTiles = new List<CityTileLinkDto>
        {
            new CityTileLinkDto(CityId: 301, TileId: 3),
            new CityTileLinkDto(CityId: 301, TileId: 4),
            new CityTileLinkDto(CityId: 301, TileId: 11),
            new CityTileLinkDto(CityId: 302, TileId: 45),
            new CityTileLinkDto(CityId: 302, TileId: 44),
            new CityTileLinkDto(CityId: 302, TileId: 37)
        };

        // Create sample city resources
        var cityResources = new List<CityResourceDto>
        {
            new CityResourceDto(CityId: 301, ResourceType: "food", Amount: 12),
            new CityResourceDto(CityId: 301, ResourceType: "production", Amount: 8),
            new CityResourceDto(CityId: 301, ResourceType: "gold", Amount: 5),
            new CityResourceDto(CityId: 302, ResourceType: "food", Amount: 8),
            new CityResourceDto(CityId: 302, ResourceType: "production", Amount: 6),
            new CityResourceDto(CityId: 302, ResourceType: "gold", Amount: 3)
        };

        var tileStates = new List<GameTileStateDto>
        {
            new GameTileStateDto(TileId: 3, ResourceType: "wheat", ResourceAmount: 70),
            new GameTileStateDto(TileId: 45, ResourceType: "iron", ResourceAmount: 40)
        };

        // Create sample unit definitions (game rules reference)
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

        // Build complete game state
        return new GameStateDto(
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
    }
}

