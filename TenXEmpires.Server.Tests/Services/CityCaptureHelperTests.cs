using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Services;

namespace TenXEmpires.Server.Tests.Services;

public class CityCaptureHelperTests
{
    [Fact]
    public async Task CaptureCityAsync_TransfersOwnership_EliminatesOldOwner_AndFinishesGame()
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TenXDbContext(options);

        var game = new Game { Id = 1, UserId = Guid.NewGuid(), MapId = 1, MapSchemaVersion = 1, Status = GameStatus.Active };
        var oldOwner = new Participant { Id = 10, GameId = 1, Kind = ParticipantKind.Ai, DisplayName = "Old", IsEliminated = false };
        var newOwner = new Participant { Id = 11, GameId = 1, Kind = ParticipantKind.Human, UserId = Guid.NewGuid(), DisplayName = "New", IsEliminated = false };
        var map = new Map { Id = 1, Code = "test", SchemaVersion = 1, Width = 5, Height = 5 };
        var tile = new MapTile { Id = 1, MapId = 1, Row = 0, Col = 0, Terrain = "plains" };
        var city = new City { Id = 100, GameId = 1, ParticipantId = oldOwner.Id, TileId = tile.Id, Hp = 0, MaxHp = 100 };

        context.Maps.Add(map);
        context.MapTiles.Add(tile);
        context.Games.Add(game);
        context.Participants.AddRange(oldOwner, newOwner);
        context.Cities.Add(city);
        await context.SaveChangesAsync();

        await CityCaptureHelper.CaptureCityAsync(context, city, newOwner.Id);
        await context.SaveChangesAsync();

        city.ParticipantId.Should().Be(newOwner.Id);
        city.Hp.Should().Be(1);
        (await context.Participants.FindAsync(oldOwner.Id))!.IsEliminated.Should().BeTrue();
        (await context.Games.FindAsync(1L))!.Status.Should().Be(GameStatus.Finished);
        (await context.Games.FindAsync(1L))!.ActiveParticipantId.Should().BeNull();
    }
}

