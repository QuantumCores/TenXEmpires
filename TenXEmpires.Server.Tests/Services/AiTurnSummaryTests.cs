using System.Text.Json;
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
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Services;

namespace TenXEmpires.Server.Tests.Services;

public class AiTurnSummaryTests
{
    [Fact(Skip = "ExecuteSqlInterpolatedAsync not supported by InMemoryDatabase. Test with real database in integration tests.")]
    public async Task EndTurnAsync_IncludesAiSummaryForHarvestAndProduction()
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var context = new TenXDbContext(options);

        var userId = Guid.NewGuid();
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

        // AI city with resources for production (stone>=10 -> slinger)
        var aiCityTile = tiles.First(t => t.Row == 1 && t.Col == 1);
        var aiCity = new City { Id = 100, GameId = 1, ParticipantId = ai.Id, TileId = aiCityTile.Id, Hp = 100, MaxHp = 100 };
        context.Cities.Add(aiCity);
        context.CityTiles.Add(new CityTile { GameId = 1, CityId = aiCity.Id, TileId = aiCityTile.Id });
        context.CityResources.Add(new CityResource { CityId = aiCity.Id, ResourceType = ResourceTypes.Stone, Amount = 10 });
        aiCityTile.ResourceType = ResourceTypes.Wood; aiCityTile.ResourceAmount = 2;

        // Unit defs
        var warrior = new UnitDefinition { Id = 1, Code = UnitTypes.Warrior, IsRanged = false, Attack = 20, Defence = 10, RangeMin = 0, RangeMax = 0, MovePoints = 2, Health = 100 };
        var slinger = new UnitDefinition { Id = 2, Code = UnitTypes.Slinger, IsRanged = true, Attack = 15, Defence = 8, RangeMin = 2, RangeMax = 3, MovePoints = 2, Health = 60 };
        context.UnitDefinitions.AddRange(warrior, slinger);

        await context.SaveChangesAsync();

        var gsLogger = Mock.Of<ILogger<GameStateService>>();
        var gs = new GameStateService(context, gsLogger);
        var idemp = new MemoryIdempotencyStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var saveSvc = new SaveService(context, Mock.Of<ILogger<SaveService>>(), idemp, gs);
        var settings = Options.Create(new GameSettings());
        var turnSvc = new TurnService(context, gs, saveSvc, idemp, settings, Mock.Of<ILogger<TurnService>>());

        await turnSvc.EndTurnAsync(userId, 1, new EndTurnCommand(), null);

        // Get last AI turn and inspect summary
        var aiTurn = await context.Turns
            .Where(t => t.GameId == 1 && t.ParticipantId == ai.Id)
            .OrderByDescending(t => t.Id)
            .FirstAsync();

        aiTurn.Summary.Should().NotBeNull();
        var doc = JsonDocument.Parse(aiTurn.Summary!);
        var root = doc.RootElement;
        root.GetProperty("harvested").TryGetProperty("wood", out var wood).Should().BeTrue();
        wood.GetInt32().Should().BeGreaterThanOrEqualTo(1);
        var produced = root.GetProperty("producedUnits");
        produced.GetArrayLength().Should().BeGreaterThan(0);
    }
}
