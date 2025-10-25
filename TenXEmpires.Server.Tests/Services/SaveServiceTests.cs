using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Services;

namespace TenXEmpires.Server.Tests.Services;

public class SaveServiceTests
{
    [Fact]
    public async Task CreateAutosaveAsync_EnforcesRingBufferOfFive()
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TenXDbContext(options);
        var logger = new Mock<ILogger<SaveService>>().Object;
        var service = new SaveService(context, logger);

        var userId = Guid.NewGuid();
        var gameId = 1L;

        context.Games.Add(new Game { Id = gameId, UserId = userId, MapId = 1, MapSchemaVersion = 1, Status = "active" });
        context.Maps.Add(new Map { Id = 1, Code = "m", SchemaVersion = 1, Width = 3, Height = 3 });
        await context.SaveChangesAsync();

        // Seed 5 autosaves
        for (int i = 0; i < 5; i++)
        {
            context.Saves.Add(new Save
            {
                UserId = userId,
                GameId = gameId,
                Kind = "autosave",
                Name = $"Autosave - Turn {i + 1}",
                TurnNo = i + 1,
                ActiveParticipantId = 1,
                SchemaVersion = 1,
                MapCode = "m",
                State = "{}",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10 - i)
            });
        }
        await context.SaveChangesAsync();

        // Create new autosave with dummy state
        var state = new GameStateDto(
            new GameStateGameDto(gameId, 6, 1, false, "active"),
            new GameStateMapDto(1, "m", 1, 3, 3),
            new List<ParticipantDto>(),
            new List<UnitInStateDto>(),
            new List<CityInStateDto>(),
            new List<CityTileLinkDto>(),
            new List<CityResourceDto>(),
            new List<UnitDefinitionDto>(),
            null);

        var id = await service.CreateAutosaveAsync(userId, gameId, 6, 1, 1, "m", state);

        id.Should().BeGreaterThan(0);
        var autosaves = await context.Saves.Where(s => s.GameId == gameId && s.Kind == "autosave").ToListAsync();
        autosaves.Count.Should().Be(5);
        autosaves.Max(s => s.TurnNo).Should().Be(6);
    }
}

