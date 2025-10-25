using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Moq;
using TenXEmpires.Server.Domain.Configuration;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Services;

namespace TenXEmpires.Server.Tests.Services;

public class TurnServiceEndTurnTests
{
    [Fact]
    public async Task EndTurnAsync_AppliesRegen_Harvests_Produces_WrapsAfterAi()
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var context = new TenXDbContext(options);

        var userId = Guid.NewGuid();
        // Map + tiles 3x3
        var map = new Map { Id = 1, Code = "m", SchemaVersion = 1, Width = 3, Height = 3 };
        context.Maps.Add(map);
        var tiles = new List<MapTile>();
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                tiles.Add(new MapTile { Id = r * 3 + c + 1, MapId = 1, Row = r, Col = c, Terrain = "plains" });
        context.MapTiles.AddRange(tiles);

        var game = new Game { Id = 1, UserId = userId, MapId = 1, MapSchemaVersion = 1, TurnNo = 1, Status = GameStatus.Active };
        var human = new Participant { Id = 10, GameId = 1, Kind = ParticipantKind.Human, UserId = userId, DisplayName = "P" };
        var ai = new Participant { Id = 11, GameId = 1, Kind = ParticipantKind.Ai, DisplayName = "AI" };
        game.ActiveParticipantId = human.Id;
        context.Games.Add(game);
        context.Participants.AddRange(human, ai);

        // Human city at (1,1)
        var cityTile = tiles.First(t => t.Row == 1 && t.Col == 1);
        var humanCity = new City { Id = 100, GameId = 1, ParticipantId = human.Id, TileId = cityTile.Id, Hp = 90, MaxHp = 100 };
        context.Cities.Add(humanCity);
        // Link city tile and resource tile for harvest (wood resource)
        var cityLink = new CityTile { GameId = 1, CityId = humanCity.Id, TileId = cityTile.Id };
        context.CityTiles.Add(cityLink);
        context.CityResources.AddRange(
            new CityResource { CityId = humanCity.Id, ResourceType = ResourceTypes.Wood, Amount = 0 },
            new CityResource { CityId = humanCity.Id, ResourceType = ResourceTypes.Stone, Amount = 10 } // enough for slinger
        );
        // Ensure city tile has resource to harvest
        cityTile.ResourceType = ResourceTypes.Wood;
        cityTile.ResourceAmount = 5;

        // Unit definitions
        var warrior = new UnitDefinition { Id = 1, Code = UnitTypes.Warrior, IsRanged = false, Attack = 20, Defence = 10, RangeMin = 0, RangeMax = 0, MovePoints = 2, Health = 100 };
        var slinger = new UnitDefinition { Id = 2, Code = UnitTypes.Slinger, IsRanged = true, Attack = 15, Defence = 8, RangeMin = 2, RangeMax = 3, MovePoints = 2, Health = 60 };
        context.UnitDefinitions.AddRange(warrior, slinger);

        // AI city and unit to ensure AI executes and we wrap turn
        var aiCityTile = tiles.First(t => t.Row == 0 && t.Col == 0);
        var aiCity = new City { Id = 101, GameId = 1, ParticipantId = ai.Id, TileId = aiCityTile.Id, Hp = 100, MaxHp = 100 };
        context.Cities.Add(aiCity);
        var aiUnitTile = tiles.First(t => t.Row == 0 && t.Col == 2);
        var aiUnit = new Unit { Id = 201, GameId = 1, ParticipantId = ai.Id, TypeId = 1, TileId = aiUnitTile.Id, Hp = 100, HasActed = false, Type = warrior, Tile = aiUnitTile };
        context.Units.Add(aiUnit);

        await context.SaveChangesAsync();

        var gameStateLogger = Mock.Of<ILogger<GameStateService>>();
        var gameStateSvc = new GameStateService(context, gameStateLogger);
        var saveLogger = Mock.Of<ILogger<SaveService>>();
        var saveSvc = new SaveService(context, saveLogger);
        var idempStore = new MemoryIdempotencyStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var settings = Options.Create(new GameSettings { CityRegenNormal = 4, CityRegenUnderSiege = 2 });
        var turnLogger = Mock.Of<ILogger<TurnService>>();
        var turnSvc = new TurnService(context, gameStateSvc, saveSvc, idempStore, settings, turnLogger);

        var response = await turnSvc.EndTurnAsync(userId, 1, new EndTurnCommand(), null);

        response.Should().NotBeNull();

        // Regen applied to human city (under siege reduces to +2); assert increased above original 90
        (await context.Cities.FindAsync(100L))!.Hp.Should().BeGreaterThan(90);
        // Harvested 1 wood and decremented tile stock
        (await context.CityResources.Where(cr => cr.CityId == 100 && cr.ResourceType == ResourceTypes.Wood).Select(cr => cr.Amount).FirstAsync()).Should().Be(1);
        (await context.MapTiles.FindAsync(cityTile.Id))!.ResourceAmount.Should().Be(4);
        // Produced unit (slinger) if spawn available; city tile is occupied by city but we allow city tile spawn; ensure at least one new unit exists
        (await context.Units.Where(u => u.GameId == 1 && u.ParticipantId == 10 && u.Id != aiUnit.Id).CountAsync()).Should().BeGreaterThan(0);
        // Autosave created (ring buffer size 5 - only 1 here)
        (await context.Saves.Where(s => s.GameId == 1 && s.Kind == "autosave").CountAsync()).Should().Be(1);
        // After AI executed, turn should wrap back to human and increment to 2
        (await context.Games.FindAsync(1L))!.TurnNo.Should().Be(2);
        (await context.Games.FindAsync(1L))!.ActiveParticipantId.Should().Be(human.Id);
        // Next participant units (human) have HasActed reset
        (await context.Units.Where(u => u.GameId == 1 && u.ParticipantId == human.Id).AllAsync(u => !u.HasActed)).Should().BeTrue();
    }
}
