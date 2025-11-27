using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Services;

namespace TenXEmpires.Server.Tests.Services;

public class ActionServiceSpawnTests
{
    [Fact]
    public async Task SpawnUnitAsync_SpawnsUnitAndMarksCityActed()
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new TenXDbContext(options);
        var userId = Guid.NewGuid();

        var map = new Map { Id = 1, Code = "test", SchemaVersion = 1, Width = 3, Height = 3 };
        var tiles = Enumerable.Range(0, 9)
            .Select(i => new MapTile
            {
                Id = i + 1,
                MapId = 1,
                Row = i / 3,
                Col = i % 3,
                Terrain = "plains",
            })
            .ToList();

        var game = new Game { Id = 1, UserId = userId, MapId = 1, MapSchemaVersion = 1, TurnNo = 1, Status = GameStatus.Active };
        var human = new Participant { Id = 10, GameId = 1, Kind = ParticipantKind.Human, UserId = userId, DisplayName = "P" };
        game.ActiveParticipantId = human.Id;

        var warrior = new UnitDefinition { Id = 1, Code = UnitTypes.Warrior, Health = 100, Attack = 20, Defence = 10, RangeMin = 0, RangeMax = 0, MovePoints = 2, IsRanged = false };

        var cityTile = tiles.Single(t => t.Row == 1 && t.Col == 1);
        var city = new City
        {
            Id = 100,
            GameId = 1,
            ParticipantId = human.Id,
            TileId = cityTile.Id,
            Hp = 100,
            MaxHp = 100,
            HasActedThisTurn = false,
            Tile = cityTile,
            CityResources = new List<CityResource>
            {
                new CityResource { CityId = 100, ResourceType = ResourceTypes.Iron, Amount = 10 },
                new CityResource { CityId = 100, ResourceType = ResourceTypes.Stone, Amount = 10 },
            }
        };

        context.Maps.Add(map);
        context.MapTiles.AddRange(tiles);
        context.Games.Add(game);
        context.Participants.Add(human);
        context.UnitDefinitions.Add(warrior);
        context.Cities.Add(city);
        await context.SaveChangesAsync();

        var stateSvc = new Mock<IGameStateService>();
        stateSvc.Setup(s => s.BuildGameStateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameStateDto(
                new GameStateGameDto(1, 1, human.Id, false, GameStatus.Active),
                new GameStateMapDto(1, "test", 1, 3, 3),
                new List<ParticipantDto>(),
                new List<UnitInStateDto>(),
                new List<CityInStateDto>(),
                new List<CityTileLinkDto>(),
                new List<CityResourceDto>(),
                new List<GameTileStateDto>(),
                new List<UnitDefinitionDto>(),
                null));

        var idemp = new Mock<IIdempotencyStore>();
        idemp.Setup(_ => _.TryGetAsync<ActionStateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionStateResponse?)null);
        idemp.Setup(_ => _.TryStoreAsync(It.IsAny<string>(), It.IsAny<ActionStateResponse>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var svc = new ActionService(context, stateSvc.Object, idemp.Object, Mock.Of<ILogger<ActionService>>());

        var response = await svc.SpawnUnitAsync(userId, 1, new SpawnUnitCommand(100, UnitTypes.Warrior), null);

        response.Should().NotBeNull();
        var spawnedUnit = await context.Units.SingleAsync(u => u.ParticipantId == human.Id && u.TypeId == warrior.Id);
        spawnedUnit.TileId.Should().Be(cityTile.Id); // city tile is free, so it should be used first
        spawnedUnit.HasActed.Should().BeFalse();

        var iron = await context.CityResources.SingleAsync(r => r.CityId == city.Id && r.ResourceType == ResourceTypes.Iron);
        iron.Amount.Should().Be(0);

        var updatedCity = await context.Cities.FindAsync(city.Id);
        updatedCity!.HasActedThisTurn.Should().BeTrue();
    }

    [Fact]
    public async Task SpawnUnitAsync_WhenNoFreeTiles_ThrowsSpawnBlockedAndDoesNotSpend()
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new TenXDbContext(options);
        var userId = Guid.NewGuid();

        var map = new Map { Id = 1, Code = "test", SchemaVersion = 1, Width = 3, Height = 3 };
        var tiles = Enumerable.Range(0, 9)
            .Select(i => new MapTile
            {
                Id = i + 1,
                MapId = 1,
                Row = i / 3,
                Col = i % 3,
                Terrain = i == 4 ? "plains" : "water" // only center tile is land
            })
            .ToList();

        var game = new Game { Id = 1, UserId = userId, MapId = 1, MapSchemaVersion = 1, TurnNo = 1, Status = GameStatus.Active };
        var human = new Participant { Id = 10, GameId = 1, Kind = ParticipantKind.Human, UserId = userId, DisplayName = "P" };
        game.ActiveParticipantId = human.Id;

        var warrior = new UnitDefinition { Id = 1, Code = UnitTypes.Warrior, Health = 100, Attack = 20, Defence = 10, RangeMin = 0, RangeMax = 0, MovePoints = 2, IsRanged = false };

        var cityTile = tiles.Single(t => t.Row == 1 && t.Col == 1);
        var city = new City
        {
            Id = 100,
            GameId = 1,
            ParticipantId = human.Id,
            TileId = cityTile.Id,
            Hp = 100,
            MaxHp = 100,
            HasActedThisTurn = false,
            Tile = cityTile,
            CityResources = new List<CityResource>
            {
                new CityResource { CityId = 100, ResourceType = ResourceTypes.Iron, Amount = 20 }
            }
        };

        // Occupy the city tile with an existing unit
        var blockingUnit = new Unit { Id = 500, GameId = 1, ParticipantId = human.Id, TypeId = warrior.Id, TileId = cityTile.Id, Hp = 100, HasActed = false, Tile = cityTile };

        context.Maps.Add(map);
        context.MapTiles.AddRange(tiles);
        context.Games.Add(game);
        context.Participants.Add(human);
        context.UnitDefinitions.Add(warrior);
        context.Cities.Add(city);
        context.Units.Add(blockingUnit);
        await context.SaveChangesAsync();

        var svc = new ActionService(
            context,
            Mock.Of<IGameStateService>(),
            Mock.Of<IIdempotencyStore>(),
            Mock.Of<ILogger<ActionService>>());

        var act = async () => await svc.SpawnUnitAsync(userId, 1, new SpawnUnitCommand(city.Id, UnitTypes.Warrior), null);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("SPAWN_BLOCKED");

        var iron = await context.CityResources.SingleAsync(r => r.CityId == city.Id && r.ResourceType == ResourceTypes.Iron);
        iron.Amount.Should().Be(20);
        (await context.Cities.FindAsync(city.Id))!.HasActedThisTurn.Should().BeFalse();
    }

    [Fact]
    public async Task SpawnUnitAsync_WhenInsufficientResources_FailsWithoutStateChange()
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new TenXDbContext(options);
        var userId = Guid.NewGuid();

        var map = new Map { Id = 1, Code = "test", SchemaVersion = 1, Width = 2, Height = 2 };
        var tiles = Enumerable.Range(0, 4)
            .Select(i => new MapTile
            {
                Id = i + 1,
                MapId = 1,
                Row = i / 2,
                Col = i % 2,
                Terrain = "plains"
            })
            .ToList();

        var game = new Game { Id = 1, UserId = userId, MapId = 1, MapSchemaVersion = 1, TurnNo = 1, Status = GameStatus.Active };
        var human = new Participant { Id = 10, GameId = 1, Kind = ParticipantKind.Human, UserId = userId, DisplayName = "P" };
        game.ActiveParticipantId = human.Id;

        var warrior = new UnitDefinition { Id = 1, Code = UnitTypes.Warrior, Health = 100, Attack = 20, Defence = 10, RangeMin = 0, RangeMax = 0, MovePoints = 2, IsRanged = false };
        var cityTile = tiles.First();

        var city = new City
        {
            Id = 100,
            GameId = 1,
            ParticipantId = human.Id,
            TileId = cityTile.Id,
            Hp = 100,
            MaxHp = 100,
            HasActedThisTurn = false,
            Tile = cityTile,
            CityResources = new List<CityResource>
            {
                new CityResource { CityId = 100, ResourceType = ResourceTypes.Iron, Amount = 5 }
            }
        };

        context.Maps.Add(map);
        context.MapTiles.AddRange(tiles);
        context.Games.Add(game);
        context.Participants.Add(human);
        context.UnitDefinitions.Add(warrior);
        context.Cities.Add(city);
        await context.SaveChangesAsync();

        var svc = new ActionService(
            context,
            Mock.Of<IGameStateService>(),
            Mock.Of<IIdempotencyStore>(),
            Mock.Of<ILogger<ActionService>>());

        var act = async () => await svc.SpawnUnitAsync(userId, 1, new SpawnUnitCommand(city.Id, UnitTypes.Warrior), null);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("INSUFFICIENT_RESOURCES");

        (await context.CityResources.SingleAsync(r => r.CityId == city.Id)).Amount.Should().Be(5);
        (await context.Cities.FindAsync(city.Id))!.HasActedThisTurn.Should().BeFalse();
        (await context.Units.CountAsync()).Should().Be(0);
    }
}
