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
    [Fact(Skip = "ExecuteSqlInterpolatedAsync not supported by InMemoryDatabase. Test with real database in integration tests.")]
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

        // Seed per-game tile states mirroring map template amounts
        foreach (var tile in tiles)
        {
            context.GameTileStates.Add(new GameTileState
            {
                GameId = game.Id,
                TileId = tile.Id,
                ResourceAmount = tile.ResourceAmount
            });
        }

        await context.SaveChangesAsync();

        var gameStateLogger = Mock.Of<ILogger<GameStateService>>();
        var gameStateSvc = new GameStateService(context, gameStateLogger);
        var saveLogger = Mock.Of<ILogger<SaveService>>();
        var idemp = new MemoryIdempotencyStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var gs = new GameStateService(context, Mock.Of<ILogger<GameStateService>>());
        var saveSvc = new SaveService(context, saveLogger, idemp, gs);
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
        (await context.GameTileStates.SingleAsync(ts => ts.GameId == game.Id && ts.TileId == cityTile.Id)).ResourceAmount.Should().Be(4);
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

    [Fact]
    public async Task HarvestCityResources_SkipsEnemyOccupiedTiles_EvenIfNotUnderSiege()
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var context = new TenXDbContext(options);

        var map = new Map { Id = 1, Code = "m", SchemaVersion = 1, Width = 4, Height = 4 };
        context.Maps.Add(map);
        var tiles = new List<MapTile>();
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                tiles.Add(new MapTile { Id = r * 4 + c + 1, MapId = 1, Row = r, Col = c, Terrain = "plains" });
            }
        }
        context.MapTiles.AddRange(tiles);

        var human = new Participant { Id = 10, GameId = 1, Kind = ParticipantKind.Human, UserId = Guid.NewGuid(), DisplayName = "Human" };
        var ai = new Participant { Id = 11, GameId = 1, Kind = ParticipantKind.Ai, DisplayName = "AI" };
        context.Participants.AddRange(human, ai);
        var game = new Game { Id = 1, UserId = human.UserId!.Value, MapId = 1, MapSchemaVersion = 1, TurnNo = 1, Status = GameStatus.Active, ActiveParticipantId = human.Id };
        context.Games.Add(game);

        var cityTile = tiles.First(t => t.Row == 0 && t.Col == 0);
        var resourceTile = tiles.First(t => t.Row == 3 && t.Col == 3); // distant tile, not adjacent
        resourceTile.ResourceType = ResourceTypes.Wood;
        resourceTile.ResourceAmount = 3;

        var city = new City { Id = 100, GameId = 1, ParticipantId = human.Id, TileId = cityTile.Id, Hp = 80, MaxHp = 100 };
        context.Cities.Add(city);

        var cityLink = new CityTile { GameId = 1, CityId = city.Id, TileId = resourceTile.Id };
        context.CityTiles.Add(cityLink);
        context.CityResources.Add(new CityResource { CityId = city.Id, ResourceType = ResourceTypes.Wood, Amount = 0 });

        var blockingUnit = new Unit
        {
            Id = 200,
            GameId = 1,
            ParticipantId = ai.Id,
            TileId = resourceTile.Id,
            Tile = resourceTile,
            Hp = 100,
            TypeId = 1,
            Type = new UnitDefinition { Id = 1, Code = UnitTypes.Warrior, Attack = 10, Defence = 10, Health = 100 }
        };
        context.UnitDefinitions.Add(blockingUnit.Type);
        context.Units.Add(blockingUnit);

        foreach (var tile in tiles)
        {
            context.GameTileStates.Add(new GameTileState
            {
                GameId = game.Id,
                TileId = tile.Id,
                ResourceAmount = tile.ResourceAmount
            });
        }

        await context.SaveChangesAsync();

        var turnService = new TurnService(context, Mock.Of<ILogger<TurnService>>());
        var method = typeof(TurnService).GetMethod("HarvestCityResources", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var loadedCity = await context.Cities
            .Include(c => c.CityResources)
            .Include(c => c.CityTiles)
                .ThenInclude(ct => ct.Tile)
            .FirstAsync(c => c.Id == city.Id);
        var allUnits = await context.Units.Include(u => u.Tile).ToListAsync();
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var overflowTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var tileStateDict = await context.GameTileStates
            .Where(ts => ts.GameId == game.Id)
            .ToDictionaryAsync(ts => ts.TileId);
        var storageCap = ResourceTypes.DefaultStorageCap;

        method!.Invoke(turnService, new object[] { loadedCity, allUnits, totals, overflowTotals, tileStateDict, storageCap });

        (await context.CityResources.Where(cr => cr.CityId == city.Id && cr.ResourceType == ResourceTypes.Wood).Select(cr => cr.Amount).FirstAsync())
            .Should().Be(0, "harvest should be blocked when enemy occupies tile");
        (await context.GameTileStates.SingleAsync(ts => ts.GameId == game.Id && ts.TileId == resourceTile.Id)).ResourceAmount.Should().Be(3, "tile stock should remain untouched");
        totals.ContainsKey(ResourceTypes.Wood).Should().BeFalse();
    }

    [Fact]
    public async Task TryExecuteAiTurnsAsync_DamagesEnemyCity_WhenInRange()
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var context = new TenXDbContext(options);

        var map = new Map { Id = 1, Code = "m", SchemaVersion = 1, Width = 3, Height = 3 };
        context.Maps.Add(map);
        var tiles = new List<MapTile>();
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                tiles.Add(new MapTile { Id = r * 3 + c + 1, MapId = map.Id, Row = r, Col = c, Terrain = "plains" });
            }
        }
        context.MapTiles.AddRange(tiles);

        var human = new Participant { Id = 1, GameId = 1, Kind = ParticipantKind.Human, UserId = Guid.NewGuid(), DisplayName = "Human" };
        var ai = new Participant { Id = 2, GameId = 1, Kind = ParticipantKind.Ai, DisplayName = "AI" };
        context.Participants.AddRange(human, ai);

        var game = new Game
        {
            Id = 1,
            UserId = human.UserId!.Value,
            MapId = map.Id,
            MapSchemaVersion = map.SchemaVersion,
            Status = GameStatus.Active,
            TurnNo = 1,
            ActiveParticipantId = ai.Id,
            Map = map
        };
        context.Games.Add(game);

        var warrior = new UnitDefinition { Id = 10, Code = UnitTypes.Warrior, IsRanged = false, Attack = 20, Defence = 10, RangeMin = 0, RangeMax = 0, MovePoints = 2, Health = 100 };
        context.UnitDefinitions.Add(warrior);

        var aiTile = tiles.First(t => t.Row == 1 && t.Col == 0);
        var aiUnit = new Unit { Id = 100, GameId = game.Id, ParticipantId = ai.Id, TileId = aiTile.Id, Hp = 100, HasActed = false, TypeId = warrior.Id };
        context.Units.Add(aiUnit);

        var cityTile = tiles.First(t => t.Row == 1 && t.Col == 1);
        var city = new City { Id = 200, GameId = game.Id, ParticipantId = human.Id, TileId = cityTile.Id, Hp = 50, MaxHp = 100 };
        context.Cities.Add(city);

        await context.SaveChangesAsync();

        var turnService = new TurnService(context, Mock.Of<ILogger<TurnService>>());
        var method = typeof(TurnService).GetMethod("TryExecuteAiTurnsAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var executed = await ((Task<bool>)method!.Invoke(turnService, new object[] { game, TimeSpan.FromSeconds(1), CancellationToken.None })!);
        executed.Should().BeTrue("AI should execute when it is the active participant");

        var reloadedCity = await context.Cities.FindAsync(city.Id);
        reloadedCity!.Hp.Should().BeLessThan(50, "AI attack should damage the enemy city when in range");
        (await context.Units.FindAsync(aiUnit.Id))!.HasActed.Should().BeTrue();
    }
}
