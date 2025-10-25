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

public class ActionServiceCaptureTests
{
    [Fact]
    public async Task MoveUnitAsync_OnDefeatedEnemyCityTile_Melee_CapturesCity()
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

        var warrior = new UnitDefinition { Id = 1, Code = UnitTypes.Warrior, IsRanged = false, Attack = 20, Defence = 10, RangeMin = 0, RangeMax = 0, MovePoints = 2, Health = 100 };
        context.UnitDefinitions.Add(warrior);

        var cityTile = tiles.First(t => t.Row == 1 && t.Col == 1);
        var city = new City { Id = 100, GameId = 1, ParticipantId = ai.Id, TileId = cityTile.Id, Hp = 0, MaxHp = 100 };
        context.Cities.Add(city);

        var unitTile = tiles.First(t => t.Row == 1 && t.Col == 0);
        var unit = new Unit { Id = 200, GameId = 1, ParticipantId = human.Id, TypeId = warrior.Id, Type = warrior, TileId = unitTile.Id, Tile = unitTile, Hp = 100 };
        context.Units.Add(unit);

        await context.SaveChangesAsync();

        var stateSvc = new Mock<IGameStateService>();
        stateSvc.Setup(s => s.BuildGameStateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameStateDto(
                new GameStateGameDto(1, 1, human.Id, false, GameStatus.Active),
                new GameStateMapDto(1, "m", 1, 3, 3),
                new List<ParticipantDto>(), new List<UnitInStateDto>(), new List<CityInStateDto>(),
                new List<CityTileLinkDto>(), new List<CityResourceDto>(), new List<UnitDefinitionDto>(), null));
        var idemp = new Mock<IIdempotencyStore>();
        idemp.Setup(_ => _.TryGetAsync<ActionStateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionStateResponse?)null);
        idemp.Setup(_ => _.TryStoreAsync(It.IsAny<string>(), It.IsAny<ActionStateResponse>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var svc = new ActionService(context, stateSvc.Object, idemp.Object, Mock.Of<ILogger<ActionService>>());

        var command = new MoveUnitCommand(unit.Id, new GridPosition(1, 1));
        var resp = await svc.MoveUnitAsync(userId, 1, command, null);

        resp.Should().NotBeNull();
        (await context.Cities.FindAsync(100L))!.ParticipantId.Should().Be(human.Id);
        (await context.Cities.FindAsync(100L))!.Hp.Should().Be(1);
    }
}
