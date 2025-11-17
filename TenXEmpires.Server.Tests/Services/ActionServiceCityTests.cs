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

public class ActionServiceCityTests
{
    [Fact]
    public async Task AttackCityAsync_WhenCitySurvives_DoesNotCapture()
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var context = new TenXDbContext(options);

        var userId = Guid.NewGuid();
        var game = new Game { Id = 1, UserId = userId, MapId = 1, MapSchemaVersion = 1, TurnNo = 1, Status = GameStatus.Active };
        var map = new Map { Id = 1, Code = "m", SchemaVersion = 1, Width = 5, Height = 5 };
        var tiles = new List<MapTile>();
        for (int r = 0; r < 5; r++)
            for (int c = 0; c < 5; c++)
                tiles.Add(new MapTile { Id = r * 5 + c + 1, MapId = 1, Row = r, Col = c, Terrain = "plains" });

        var human = new Participant { Id = 10, GameId = 1, Kind = ParticipantKind.Human, UserId = userId, DisplayName = "P" };
        var ai = new Participant { Id = 11, GameId = 1, Kind = ParticipantKind.Ai, DisplayName = "AI" };

        game.ActiveParticipantId = human.Id;

        var warrior = new UnitDefinition { Id = 1, Code = UnitTypes.Warrior, IsRanged = false, Attack = 20, Defence = 10, RangeMin = 0, RangeMax = 0, MovePoints = 2, Health = 100 };

        var cityTile = tiles.First(t => t.Row == 2 && t.Col == 2);
        var city = new City { Id = 100, GameId = 1, ParticipantId = ai.Id, TileId = cityTile.Id, Hp = 100, MaxHp = 100 };

        var unitTile = tiles.First(t => t.Row == 2 && t.Col == 1); // adjacent (melee range)
        var unit = new Unit { Id = 200, GameId = 1, ParticipantId = human.Id, TypeId = 1, TileId = unitTile.Id, Hp = 100, HasActed = false, Type = warrior, Tile = unitTile };

        context.Maps.Add(map);
        context.MapTiles.AddRange(tiles);
        context.Games.Add(game);
        context.Participants.AddRange(human, ai);
        context.UnitDefinitions.Add(warrior);
        context.Cities.Add(city);
        context.Units.Add(unit);
        await context.SaveChangesAsync();

        var stateSvc = new Mock<IGameStateService>();
        stateSvc.Setup(s => s.BuildGameStateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameStateDto(
                new GameStateGameDto(1, 1, human.Id, false, GameStatus.Active),
                new GameStateMapDto(1, "m", 1, 5, 5),
                new List<ParticipantDto>(), new List<UnitInStateDto>(), new List<CityInStateDto>(),
                new List<CityTileLinkDto>(), new List<CityResourceDto>(), new List<UnitDefinitionDto>(), null));

        var idemp = new Mock<IIdempotencyStore>();
        idemp.Setup(_ => _.TryGetAsync<ActionStateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionStateResponse?)null);
        idemp.Setup(_ => _.TryStoreAsync(It.IsAny<string>(), It.IsAny<ActionStateResponse>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var svc = new ActionService(context, stateSvc.Object, idemp.Object, Mock.Of<ILogger<ActionService>>());

        var response = await svc.AttackCityAsync(userId, 1, unit.Id, city.Id, null);

        response.Should().NotBeNull();
        var updatedCity = await context.Cities.FindAsync(100L);
        updatedCity!.Hp.Should().BeGreaterThan(0);
        updatedCity.Hp.Should().BeLessThan(100);
        updatedCity.ParticipantId.Should().Be(ai.Id); // still enemy when HP remains

        var updatedUnit = await context.Units.FindAsync(200L);
        updatedUnit!.HasActed.Should().BeTrue();

        var updatedGame = await context.Games.FindAsync(1L);
        updatedGame!.Status.Should().Be(GameStatus.Active);
        updatedGame.ActiveParticipantId.Should().Be(human.Id);
    }

    [Fact]
    public async Task AttackCityAsync_WhenCityDefeated_CapturesCityAndFinishesGame()
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var context = new TenXDbContext(options);

        var userId = Guid.NewGuid();
        var game = new Game { Id = 1, UserId = userId, MapId = 1, MapSchemaVersion = 1, TurnNo = 1, Status = GameStatus.Active };
        var map = new Map { Id = 1, Code = "m", SchemaVersion = 1, Width = 5, Height = 5 };
        var tiles = new List<MapTile>();
        for (int r = 0; r < 5; r++)
            for (int c = 0; c < 5; c++)
                tiles.Add(new MapTile { Id = r * 5 + c + 1, MapId = 1, Row = r, Col = c, Terrain = "plains" });

        var human = new Participant { Id = 10, GameId = 1, Kind = ParticipantKind.Human, UserId = userId, DisplayName = "P" };
        var ai = new Participant { Id = 11, GameId = 1, Kind = ParticipantKind.Ai, DisplayName = "AI" };

        game.ActiveParticipantId = human.Id;

        var warrior = new UnitDefinition { Id = 1, Code = UnitTypes.Warrior, IsRanged = false, Attack = 20, Defence = 10, RangeMin = 0, RangeMax = 0, MovePoints = 2, Health = 100 };

        var cityTile = tiles.First(t => t.Row == 2 && t.Col == 2);
        var city = new City { Id = 100, GameId = 1, ParticipantId = ai.Id, TileId = cityTile.Id, Hp = 5, MaxHp = 100 };

        var unitTile = tiles.First(t => t.Row == 2 && t.Col == 1); // adjacent (melee range)
        var unit = new Unit { Id = 200, GameId = 1, ParticipantId = human.Id, TypeId = 1, TileId = unitTile.Id, Hp = 100, HasActed = false, Type = warrior, Tile = unitTile };

        context.Maps.Add(map);
        context.MapTiles.AddRange(tiles);
        context.Games.Add(game);
        context.Participants.AddRange(human, ai);
        context.UnitDefinitions.Add(warrior);
        context.Cities.Add(city);
        context.Units.Add(unit);
        await context.SaveChangesAsync();

        var stateSvc = new Mock<IGameStateService>();
        stateSvc.Setup(s => s.BuildGameStateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameStateDto(
                new GameStateGameDto(1, 1, human.Id, false, GameStatus.Active),
                new GameStateMapDto(1, "m", 1, 5, 5),
                new List<ParticipantDto>(), new List<UnitInStateDto>(), new List<CityInStateDto>(),
                new List<CityTileLinkDto>(), new List<CityResourceDto>(), new List<UnitDefinitionDto>(), null));

        var idemp = new Mock<IIdempotencyStore>();
        idemp.Setup(_ => _.TryGetAsync<ActionStateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionStateResponse?)null);
        idemp.Setup(_ => _.TryStoreAsync(It.IsAny<string>(), It.IsAny<ActionStateResponse>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var svc = new ActionService(context, stateSvc.Object, idemp.Object, Mock.Of<ILogger<ActionService>>());

        var response = await svc.AttackCityAsync(userId, 1, unit.Id, city.Id, null);

        response.Should().NotBeNull();

        var capturedCity = await context.Cities.FindAsync(100L);
        capturedCity!.ParticipantId.Should().Be(human.Id);
        capturedCity.Hp.Should().Be(1);

        var updatedGame = await context.Games.FindAsync(1L);
        updatedGame!.Status.Should().Be(GameStatus.Finished);
        updatedGame.ActiveParticipantId.Should().BeNull();
        updatedGame.FinishedAt.Should().NotBeNull();

        var eliminatedAi = await context.Participants.FindAsync(ai.Id);
        eliminatedAi!.IsEliminated.Should().BeTrue();

        var player = await context.Participants.FindAsync(human.Id);
        player!.IsEliminated.Should().BeFalse();
    }
}
