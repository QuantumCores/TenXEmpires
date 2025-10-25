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
        var service = new SaveService(
            context,
            logger,
            new MemoryIdempotencyStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new GameStateService(context, Mock.Of<ILogger<GameStateService>>()));

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

    [Fact]
    public async Task ListSavesAsync_WithNoSaves_ShouldReturnEmptyLists()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TenXDbContext(options);
        var logger = new Mock<ILogger<SaveService>>().Object;
        var service = new SaveService(
            context,
            logger,
            new MemoryIdempotencyStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new GameStateService(context, Mock.Of<ILogger<GameStateService>>()));

        var gameId = 1L;

        // Act
        var result = await service.ListSavesAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        result.Manual.Should().BeEmpty();
        result.Autosaves.Should().BeEmpty();
    }

    [Fact]
    public async Task ListSavesAsync_WithOnlyManualSaves_ShouldReturnManualSavesOnly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TenXDbContext(options);
        var logger = new Mock<ILogger<SaveService>>().Object;
        var service = new SaveService(
            context,
            logger,
            new MemoryIdempotencyStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new GameStateService(context, Mock.Of<ILogger<GameStateService>>()));

        var userId = Guid.NewGuid();
        var gameId = 1L;

        // Add manual saves
        context.Saves.AddRange(
            new Save
            {
                Id = 1,
                UserId = userId,
                GameId = gameId,
                Kind = "manual",
                Name = "Before battle",
                Slot = 1,
                TurnNo = 5,
                ActiveParticipantId = 1,
                SchemaVersion = 1,
                MapCode = "m",
                State = "{}",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
            },
            new Save
            {
                Id = 2,
                UserId = userId,
                GameId = gameId,
                Kind = "manual",
                Name = "Victory point",
                Slot = 2,
                TurnNo = 10,
                ActiveParticipantId = 1,
                SchemaVersion = 1,
                MapCode = "m",
                State = "{}",
                CreatedAt = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        // Act
        var result = await service.ListSavesAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        result.Manual.Should().HaveCount(2);
        result.Manual[0].Id.Should().Be(2); // Most recent first
        result.Manual[0].Name.Should().Be("Victory point");
        result.Manual[0].Slot.Should().Be(2);
        result.Manual[0].TurnNo.Should().Be(10);
        result.Manual[1].Id.Should().Be(1);
        result.Manual[1].Name.Should().Be("Before battle");
        result.Autosaves.Should().BeEmpty();
    }

    [Fact]
    public async Task ListSavesAsync_WithOnlyAutosaves_ShouldReturnAutosavesOnly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TenXDbContext(options);
        var logger = new Mock<ILogger<SaveService>>().Object;
        var service = new SaveService(
            context,
            logger,
            new MemoryIdempotencyStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new GameStateService(context, Mock.Of<ILogger<GameStateService>>()));

        var userId = Guid.NewGuid();
        var gameId = 1L;

        // Add autosaves
        context.Saves.AddRange(
            new Save
            {
                Id = 10,
                UserId = userId,
                GameId = gameId,
                Kind = "autosave",
                Name = "Autosave - Turn 3",
                Slot = null,
                TurnNo = 3,
                ActiveParticipantId = 1,
                SchemaVersion = 1,
                MapCode = "m",
                State = "{}",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
            },
            new Save
            {
                Id = 11,
                UserId = userId,
                GameId = gameId,
                Kind = "autosave",
                Name = "Autosave - Turn 4",
                Slot = null,
                TurnNo = 4,
                ActiveParticipantId = 1,
                SchemaVersion = 1,
                MapCode = "m",
                State = "{}",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
            },
            new Save
            {
                Id = 12,
                UserId = userId,
                GameId = gameId,
                Kind = "autosave",
                Name = "Autosave - Turn 5",
                Slot = null,
                TurnNo = 5,
                ActiveParticipantId = 1,
                SchemaVersion = 1,
                MapCode = "m",
                State = "{}",
                CreatedAt = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        // Act
        var result = await service.ListSavesAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        result.Manual.Should().BeEmpty();
        result.Autosaves.Should().HaveCount(3);
        result.Autosaves[0].Id.Should().Be(12); // Most recent first
        result.Autosaves[0].TurnNo.Should().Be(5);
        result.Autosaves[1].Id.Should().Be(11);
        result.Autosaves[1].TurnNo.Should().Be(4);
        result.Autosaves[2].Id.Should().Be(10);
        result.Autosaves[2].TurnNo.Should().Be(3);
    }

    [Fact]
    public async Task ListSavesAsync_WithMixedSaves_ShouldReturnBothGroupsSeparately()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TenXDbContext(options);
        var logger = new Mock<ILogger<SaveService>>().Object;
        var service = new SaveService(
            context,
            logger,
            new MemoryIdempotencyStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new GameStateService(context, Mock.Of<ILogger<GameStateService>>()));

        var userId = Guid.NewGuid();
        var gameId = 1L;

        // Add mixed saves
        context.Saves.AddRange(
            new Save
            {
                Id = 1,
                UserId = userId,
                GameId = gameId,
                Kind = "manual",
                Name = "Strategic save",
                Slot = 1,
                TurnNo = 7,
                ActiveParticipantId = 1,
                SchemaVersion = 1,
                MapCode = "m",
                State = "{}",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-3)
            },
            new Save
            {
                Id = 10,
                UserId = userId,
                GameId = gameId,
                Kind = "autosave",
                Name = "Autosave - Turn 8",
                Slot = null,
                TurnNo = 8,
                ActiveParticipantId = 1,
                SchemaVersion = 1,
                MapCode = "m",
                State = "{}",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
            },
            new Save
            {
                Id = 2,
                UserId = userId,
                GameId = gameId,
                Kind = "manual",
                Name = "Before final push",
                Slot = 2,
                TurnNo = 9,
                ActiveParticipantId = 1,
                SchemaVersion = 1,
                MapCode = "m",
                State = "{}",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
            },
            new Save
            {
                Id = 11,
                UserId = userId,
                GameId = gameId,
                Kind = "autosave",
                Name = "Autosave - Turn 10",
                Slot = null,
                TurnNo = 10,
                ActiveParticipantId = 1,
                SchemaVersion = 1,
                MapCode = "m",
                State = "{}",
                CreatedAt = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        // Act
        var result = await service.ListSavesAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        result.Manual.Should().HaveCount(2);
        result.Manual[0].Id.Should().Be(2); // Most recent first
        result.Manual[0].Name.Should().Be("Before final push");
        result.Manual[1].Id.Should().Be(1);
        result.Manual[1].Name.Should().Be("Strategic save");
        
        result.Autosaves.Should().HaveCount(2);
        result.Autosaves[0].Id.Should().Be(11); // Most recent first
        result.Autosaves[0].TurnNo.Should().Be(10);
        result.Autosaves[1].Id.Should().Be(10);
        result.Autosaves[1].TurnNo.Should().Be(8);
    }

    [Fact]
    public async Task ListSavesAsync_WithMultipleGames_ShouldOnlyReturnSavesForSpecifiedGame()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TenXDbContext(options);
        var logger = new Mock<ILogger<SaveService>>().Object;
        var service = new SaveService(
            context,
            logger,
            new MemoryIdempotencyStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new GameStateService(context, Mock.Of<ILogger<GameStateService>>()));

        var userId = Guid.NewGuid();
        var gameId1 = 1L;
        var gameId2 = 2L;

        // Add saves for both games
        context.Saves.AddRange(
            new Save
            {
                Id = 1,
                UserId = userId,
                GameId = gameId1,
                Kind = "manual",
                Name = "Game 1 save",
                Slot = 1,
                TurnNo = 5,
                ActiveParticipantId = 1,
                SchemaVersion = 1,
                MapCode = "m",
                State = "{}",
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Save
            {
                Id = 2,
                UserId = userId,
                GameId = gameId2,
                Kind = "manual",
                Name = "Game 2 save",
                Slot = 1,
                TurnNo = 3,
                ActiveParticipantId = 1,
                SchemaVersion = 1,
                MapCode = "m",
                State = "{}",
                CreatedAt = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        // Act
        var result = await service.ListSavesAsync(gameId1);

        // Assert
        result.Should().NotBeNull();
        result.Manual.Should().HaveCount(1);
        result.Manual[0].Id.Should().Be(1);
        result.Manual[0].Name.Should().Be("Game 1 save");
    }
}

public class SaveServiceDeleteTests
{
    [Fact]
    public async Task DeleteManualAsync_ReturnsTrue_AndDeletes_WhenSaveExists()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TenXDbContext(options);
        var logger = new Mock<ILogger<SaveService>>().Object;
        var service = new SaveService(
            context,
            logger,
            new MemoryIdempotencyStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new GameStateService(context, Mock.Of<ILogger<GameStateService>>()));

        var userId = Guid.NewGuid();
        var gameId = 1L;
        context.Saves.Add(new Save
        {
            UserId = userId,
            GameId = gameId,
            Kind = "manual",
            Name = "Test manual",
            Slot = 2,
            TurnNo = 1,
            ActiveParticipantId = 1,
            SchemaVersion = 1,
            MapCode = "m",
            State = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        // Act
        var result = await service.DeleteManualAsync(gameId, 2, null);

        // Assert
        result.Should().BeTrue();
        (await context.Saves.CountAsync(s => s.GameId == gameId && s.Kind == "manual" && s.Slot == 2)).Should().Be(0);
    }

    [Fact]
    public async Task DeleteManualAsync_ReturnsFalse_WhenSaveDoesNotExist()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TenXDbContext(options);
        var logger = new Mock<ILogger<SaveService>>().Object;
        var service = new SaveService(
            context,
            logger,
            new MemoryIdempotencyStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new GameStateService(context, Mock.Of<ILogger<GameStateService>>()));

        var gameId = 1L;

        // Act
        var result = await service.DeleteManualAsync(gameId, 1, null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteManualAsync_ThrowsArgumentException_ForInvalidSlot()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TenXDbContext(options);
        var logger = new Mock<ILogger<SaveService>>().Object;
        var service = new SaveService(
            context,
            logger,
            new MemoryIdempotencyStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new GameStateService(context, Mock.Of<ILogger<GameStateService>>()));

        // Act
        var act = async () => await service.DeleteManualAsync(1, 0, null);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteManualAsync_Idempotency_CachesResult_ForDeletedAndNotFound()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TenXDbContext(options);
        var logger = new Mock<ILogger<SaveService>>().Object;
        var service = new SaveService(
            context,
            logger,
            new MemoryIdempotencyStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new GameStateService(context, Mock.Of<ILogger<GameStateService>>()));

        var userId = Guid.NewGuid();
        var gameId = 10L;
        var key = "idem-1";

        // Seed a manual save at slot 1
        context.Saves.Add(new Save
        {
            UserId = userId,
            GameId = gameId,
            Kind = "manual",
            Name = "Test",
            Slot = 1,
            TurnNo = 1,
            ActiveParticipantId = 1,
            SchemaVersion = 1,
            MapCode = "m",
            State = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        // First call deletes and stores "deleted"
        var first = await service.DeleteManualAsync(gameId, 1, key);
        first.Should().BeTrue();

        // Second call should return cached true even though save is gone
        var second = await service.DeleteManualAsync(gameId, 1, key);
        second.Should().BeTrue();

        // Now test not-found caching with a different slot/key
        var key2 = "idem-2";
        var nf1 = await service.DeleteManualAsync(gameId, 2, key2);
        nf1.Should().BeFalse();

        // Even if we add a save afterward, the same key should return cached false and not delete it
        context.Saves.Add(new Save
        {
            UserId = userId,
            GameId = gameId,
            Kind = "manual",
            Name = "Later",
            Slot = 2,
            TurnNo = 1,
            ActiveParticipantId = 1,
            SchemaVersion = 1,
            MapCode = "m",
            State = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var nf2 = await service.DeleteManualAsync(gameId, 2, key2);
        nf2.Should().BeFalse();

        (await context.Saves.CountAsync(s => s.GameId == gameId && s.Kind == "manual" && s.Slot == 2)).Should().Be(1);
    }

}

