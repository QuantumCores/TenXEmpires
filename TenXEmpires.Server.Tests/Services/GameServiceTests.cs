using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenXEmpires.Server.Domain.Configuration;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Domain.Utilities;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Services;
using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Tests.Services;

public class GameServiceTests : IDisposable
{
    private readonly TenXDbContext _context;
    private readonly GameService _service;
    private readonly Mock<ILogger<GameService>> _loggerMock;
    private readonly Mock<IIdempotencyStore> _idempotencyStoreMock;
    private readonly Mock<IAiNameGenerator> _aiNameGeneratorMock;
    private readonly Mock<IGameSeedingService> _gameSeedingServiceMock;
    private readonly Mock<IGameStateService> _gameStateServiceMock;
    private readonly Guid _testUserId;

    public GameServiceTests()
    {
        // Setup in-memory database - configure warnings to ignore transaction warnings
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new TenXDbContext(options);
        _loggerMock = new Mock<ILogger<GameService>>();
        _idempotencyStoreMock = new Mock<IIdempotencyStore>();
        _aiNameGeneratorMock = new Mock<IAiNameGenerator>();
        _gameSeedingServiceMock = new Mock<IGameSeedingService>();
        _gameStateServiceMock = new Mock<IGameStateService>();

        var gameSettings = Options.Create(new GameSettings
        {
            MaxActiveGamesPerUser = 10,
            AcceptedMapSchemaVersion = 1,
            DefaultMapCode = "standard_6x8"
        });

        _service = new GameService(
            _context,
            _loggerMock.Object,
            _idempotencyStoreMock.Object,
            _aiNameGeneratorMock.Object,
            _gameSeedingServiceMock.Object,
            _gameStateServiceMock.Object,
            gameSettings);

        _testUserId = Guid.NewGuid();

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var now = DateTimeOffset.UtcNow;
        
        _context.Games.AddRange(
            new Game
            {
                Id = 1,
                UserId = _testUserId,
                MapId = 1,
                MapSchemaVersion = 1,
                TurnNo = 1,
                Status = "active",
                StartedAt = now.AddDays(-5),
                LastTurnAt = now.AddDays(-1),
                RngSeed = 12345,
                Settings = "{}"
            },
            new Game
            {
                Id = 2,
                UserId = _testUserId,
                MapId = 1,
                MapSchemaVersion = 1,
                TurnNo = 10,
                Status = "finished",
                StartedAt = now.AddDays(-10),
                LastTurnAt = now.AddDays(-3),
                FinishedAt = now.AddDays(-3),
                RngSeed = 12346,
                Settings = "{}"
            },
            new Game
            {
                Id = 3,
                UserId = _testUserId,
                MapId = 1,
                MapSchemaVersion = 1,
                TurnNo = 5,
                Status = "active",
                StartedAt = now.AddDays(-2),
                LastTurnAt = now,
                RngSeed = 12347,
                Settings = "{}"
            },
            new Game
            {
                Id = 4,
                UserId = Guid.NewGuid(), // Different user
                MapId = 1,
                MapSchemaVersion = 1,
                TurnNo = 1,
                Status = "active",
                StartedAt = now.AddDays(-1),
                LastTurnAt = now,
                RngSeed = 12348,
                Settings = "{}"
            }
        );
        
        _context.SaveChanges();
    }

    [Fact]
    public async Task ListGamesAsync_WithDefaultParameters_ShouldReturnAllUserGames()
    {
        // Arrange
        var query = new ListGamesQuery();

        // Act
        var result = await _service.ListGamesAsync(_testUserId, query);

        // Assert
        result.Items.Should().HaveCount(3); // Only user's games
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Total.Should().Be(3);
        result.Items.Should().OnlyContain(g => g.Id != 4); // Excludes other user's game
    }

    [Fact]
    public async Task ListGamesAsync_WithActiveStatus_ShouldFilterActiveGames()
    {
        // Arrange
        var query = new ListGamesQuery { Status = "active" };

        // Act
        var result = await _service.ListGamesAsync(_testUserId, query);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(g => g.Status == "active");
        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task ListGamesAsync_WithFinishedStatus_ShouldFilterFinishedGames()
    {
        // Arrange
        var query = new ListGamesQuery { Status = "finished" };

        // Act
        var result = await _service.ListGamesAsync(_testUserId, query);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items.Should().OnlyContain(g => g.Status == "finished");
        result.Items.First().Id.Should().Be(2);
    }

    [Fact]
    public async Task ListGamesAsync_WithInvalidStatus_ShouldThrowArgumentException()
    {
        // Arrange
        var query = new ListGamesQuery { Status = "invalid" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ListGamesAsync(_testUserId, query));
        
        exception.ParamName.Should().Be("Status");
        exception.Message.Should().Contain("Invalid status");
    }

    [Fact]
    public async Task ListGamesAsync_SortByStartedAtAsc_ShouldReturnGamesInCorrectOrder()
    {
        // Arrange
        var query = new ListGamesQuery { Sort = "startedAt", Order = "asc" };

        // Act
        var result = await _service.ListGamesAsync(_testUserId, query);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items[0].Id.Should().Be(2); // Oldest
        result.Items[1].Id.Should().Be(1);
        result.Items[2].Id.Should().Be(3); // Newest
        result.Items.Should().BeInAscendingOrder(g => g.StartedAt);
    }

    [Fact]
    public async Task ListGamesAsync_SortByStartedAtDesc_ShouldReturnGamesInCorrectOrder()
    {
        // Arrange
        var query = new ListGamesQuery { Sort = "startedAt", Order = "desc" };

        // Act
        var result = await _service.ListGamesAsync(_testUserId, query);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items[0].Id.Should().Be(3); // Newest
        result.Items[1].Id.Should().Be(1);
        result.Items[2].Id.Should().Be(2); // Oldest
        result.Items.Should().BeInDescendingOrder(g => g.StartedAt);
    }

    [Fact]
    public async Task ListGamesAsync_SortByLastTurnAtDesc_ShouldReturnGamesInCorrectOrder()
    {
        // Arrange
        var query = new ListGamesQuery { Sort = "lastTurnAt", Order = "desc" };

        // Act
        var result = await _service.ListGamesAsync(_testUserId, query);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items[0].Id.Should().Be(3); // Most recent turn
        result.Items[1].Id.Should().Be(1);
        result.Items[2].Id.Should().Be(2); // Oldest turn
    }

    [Fact]
    public async Task ListGamesAsync_SortByTurnNoDesc_ShouldReturnGamesInCorrectOrder()
    {
        // Arrange
        var query = new ListGamesQuery { Sort = "turnNo", Order = "desc" };

        // Act
        var result = await _service.ListGamesAsync(_testUserId, query);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items[0].TurnNo.Should().Be(10);
        result.Items[1].TurnNo.Should().Be(5);
        result.Items[2].TurnNo.Should().Be(1);
    }

    [Fact]
    public async Task ListGamesAsync_WithInvalidSortField_ShouldThrowArgumentException()
    {
        // Arrange
        var query = new ListGamesQuery { Sort = "invalidField" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ListGamesAsync(_testUserId, query));
        
        exception.ParamName.Should().Be("Sort");
        exception.Message.Should().Contain("Invalid sort field");
    }

    [Fact]
    public async Task ListGamesAsync_WithInvalidOrder_ShouldThrowArgumentException()
    {
        // Arrange
        var query = new ListGamesQuery { Order = "sideways" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ListGamesAsync(_testUserId, query));
        
        exception.ParamName.Should().Be("Order");
        exception.Message.Should().Contain("Invalid order");
    }

    [Fact]
    public async Task ListGamesAsync_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var query = new ListGamesQuery { Page = 2, PageSize = 2 };

        // Act
        var result = await _service.ListGamesAsync(_testUserId, query);

        // Assert
        result.Items.Should().HaveCount(1); // Third item
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Total.Should().Be(3);
    }

    [Fact]
    public async Task ListGamesAsync_WithPageSizeLargerThanTotal_ShouldReturnAllItems()
    {
        // Arrange
        var query = new ListGamesQuery { Page = 1, PageSize = 100 };

        // Act
        var result = await _service.ListGamesAsync(_testUserId, query);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Total.Should().Be(3);
    }

    [Fact]
    public async Task ListGamesAsync_WithPageBeyondTotal_ShouldReturnEmptyItems()
    {
        // Arrange
        var query = new ListGamesQuery { Page = 10, PageSize = 20 };

        // Act
        var result = await _service.ListGamesAsync(_testUserId, query);

        // Assert
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(3);
    }

    [Fact]
    public async Task ListGamesAsync_WithNoGamesForUser_ShouldReturnEmptyResult()
    {
        // Arrange
        var emptyUserId = Guid.NewGuid();
        var query = new ListGamesQuery();

        // Act
        var result = await _service.ListGamesAsync(emptyUserId, query);

        // Assert
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task ListGamesAsync_WithMultipleFilters_ShouldApplyAllFilters()
    {
        // Arrange
        var query = new ListGamesQuery 
        { 
            Status = "active",
            Sort = "turnNo",
            Order = "asc",
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await _service.ListGamesAsync(_testUserId, query);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(g => g.Status == "active");
        result.Items[0].TurnNo.Should().BeLessThan(result.Items[1].TurnNo);
    }

    [Fact]
    public async Task ListGamesAsync_CaseInsensitiveStatus_ShouldWork()
    {
        // Arrange
        var query = new ListGamesQuery { Status = "ACTIVE" };

        // Act
        var result = await _service.ListGamesAsync(_testUserId, query);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(g => g.Status == "active");
    }

    [Fact]
    public async Task ListGamesAsync_CaseInsensitiveSortAndOrder_ShouldWork()
    {
        // Arrange
        var query = new ListGamesQuery { Sort = "STARTEDAT", Order = "ASC" };

        // Act
        var result = await _service.ListGamesAsync(_testUserId, query);

        // Assert
        result.Items.Should().BeInAscendingOrder(g => g.StartedAt);
    }

    [Fact]
    public async Task CreateGameAsync_WithValidCommand_ShouldCreateGameSuccessfully()
    {
        // Arrange
        var newUserId = Guid.NewGuid();
        var command = new CreateGameCommand("standard_6x8", null, "TestPlayer");
        
        // Setup test map
        var testMap = new Map
        {
            Id = 100,
            Code = "standard_6x8",
            SchemaVersion = 1,
            Width = 8,
            Height = 6
        };
        _context.Maps.Add(testMap);
        await _context.SaveChangesAsync();

        // Setup mocks
        _aiNameGeneratorMock.Setup(x => x.GenerateName(It.IsAny<long>()))
            .Returns("Julius Caesar");

        var expectedGameState = new GameStateDto(
            new GameStateGameDto(1, 1, 1, false, "active"),
            new GameStateMapDto(100, "standard_6x8", 1, 8, 6),
            new List<ParticipantDto>(),
            new List<UnitInStateDto>(),
            new List<CityInStateDto>(),
            new List<CityTileLinkDto>(),
            new List<CityResourceDto>(),
            new List<UnitDefinitionDto>(),
            null);

        _gameStateServiceMock.Setup(x => x.BuildGameStateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedGameState);

        // Act
        var result = await _service.CreateGameAsync(newUserId, command, null, default);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.State.Should().NotBeNull();

        // Verify game was created in database
        var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == result.Id);
        game.Should().NotBeNull();
        game!.UserId.Should().Be(newUserId);
        game.Status.Should().Be("active");
        game.TurnNo.Should().Be(1);
        game.MapId.Should().Be(100);

        // Verify participants were created
        var participants = await _context.Participants.Where(p => p.GameId == game.Id).ToListAsync();
        participants.Should().HaveCount(2);
        participants.Should().Contain(p => p.Kind == "human" && p.DisplayName == "TestPlayer");
        participants.Should().Contain(p => p.Kind == "ai" && p.DisplayName == "Julius Caesar");

        // Verify game seeding was called
        _gameSeedingServiceMock.Verify(
            x => x.SeedGameEntitiesAsync(
                It.IsAny<long>(),
                100,
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateGameAsync_WithDefaultDisplayName_ShouldUsePlayer()
    {
        // Arrange
        var newUserId = Guid.NewGuid();
        var command = new CreateGameCommand("standard_6x8", null, null);
        
        var testMap = new Map
        {
            Id = 101,
            Code = "standard_6x8",
            SchemaVersion = 1,
            Width = 8,
            Height = 6
        };
        _context.Maps.Add(testMap);
        await _context.SaveChangesAsync();

        _aiNameGeneratorMock.Setup(x => x.GenerateName(It.IsAny<long>())).Returns("Ramses II");
        _gameStateServiceMock.Setup(x => x.BuildGameStateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameStateDto(
                new GameStateGameDto(1, 1, 1, false, "active"),
                new GameStateMapDto(101, "standard_6x8", 1, 8, 6),
                Array.Empty<ParticipantDto>(),
                Array.Empty<UnitInStateDto>(),
                Array.Empty<CityInStateDto>(),
                Array.Empty<CityTileLinkDto>(),
                Array.Empty<CityResourceDto>(),
                Array.Empty<UnitDefinitionDto>(),
                null));

        // Act
        var result = await _service.CreateGameAsync(newUserId, command, null, default);

        // Assert
        var humanParticipant = await _context.Participants
            .FirstOrDefaultAsync(p => p.GameId == result.Id && p.Kind == "human");
        humanParticipant.Should().NotBeNull();
        humanParticipant!.DisplayName.Should().Be("Player");
    }

    [Fact]
    public async Task CreateGameAsync_WithIdempotencyKey_ShouldReturnCachedResponseIfExists()
    {
        // Arrange
        var newUserId = Guid.NewGuid();
        var command = new CreateGameCommand("standard_6x8", null, "Player");
        var idempotencyKey = "test-key-123";
        
        var cachedResponse = new GameCreatedResponse(
            999,
            new GameStateDto(
                new GameStateGameDto(999, 1, 1, false, "active"),
                new GameStateMapDto(1, "standard_6x8", 1, 8, 6),
                Array.Empty<ParticipantDto>(),
                Array.Empty<UnitInStateDto>(),
                Array.Empty<CityInStateDto>(),
                Array.Empty<CityTileLinkDto>(),
                Array.Empty<CityResourceDto>(),
                Array.Empty<UnitDefinitionDto>(),
                null));

        _idempotencyStoreMock
            .Setup(x => x.TryGetAsync<GameCreatedResponse>($"create-game:{newUserId}:{idempotencyKey}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        // Act
        var result = await _service.CreateGameAsync(newUserId, command, idempotencyKey, default);

        // Assert
        result.Should().Be(cachedResponse);
        result.Id.Should().Be(999);
        
        // Verify no new game was created
        var gameCount = await _context.Games.CountAsync();
        gameCount.Should().Be(4); // Only the seeded games
    }

    [Fact]
    public async Task CreateGameAsync_WhenGameLimitReached_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var limitedUserId = Guid.NewGuid();
        
        // Create 10 active games for this user (the limit)
        for (int i = 0; i < 10; i++)
        {
            _context.Games.Add(new Game
            {
                UserId = limitedUserId,
                MapId = 1,
                MapSchemaVersion = 1,
                TurnNo = 1,
                Status = "active",
                StartedAt = DateTimeOffset.UtcNow,
                RngSeed = 12345 + i,
                Settings = "{}"
            });
        }
        await _context.SaveChangesAsync();

        var command = new CreateGameCommand("standard_6x8", null, "Player");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateGameAsync(limitedUserId, command, null, default));
        
        exception.Message.Should().Contain("Game limit reached");
        exception.Message.Should().Contain("10 active games");
    }

    [Fact]
    public async Task CreateGameAsync_WithInvalidMapCode_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var newUserId = Guid.NewGuid();
        var command = new CreateGameCommand("non_existent_map", null, "Player");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateGameAsync(newUserId, command, null, default));
        
        exception.Message.Should().Contain("Map with code 'non_existent_map' not found");
    }

    [Fact]
    public async Task CreateGameAsync_WithMapSchemaVersionMismatch_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var newUserId = Guid.NewGuid();
        var command = new CreateGameCommand("old_map", null, "Player");
        
        var oldMap = new Map
        {
            Id = 102,
            Code = "old_map",
            SchemaVersion = 2, // Mismatch with accepted version (1)
            Width = 8,
            Height = 6
        };
        _context.Maps.Add(oldMap);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateGameAsync(newUserId, command, null, default));
        
        exception.Message.Should().Contain("Map schema version mismatch");
        exception.Message.Should().Contain("Expected version 1");
        exception.Message.Should().Contain("has version 2");
    }

    [Fact]
    public async Task CreateGameAsync_OnSuccess_ShouldStoreInIdempotencyCache()
    {
        // Arrange
        var newUserId = Guid.NewGuid();
        var command = new CreateGameCommand("standard_6x8", null, "Player");
        var idempotencyKey = "test-key-456";
        
        var testMap = new Map
        {
            Id = 103,
            Code = "standard_6x8",
            SchemaVersion = 1,
            Width = 8,
            Height = 6
        };
        _context.Maps.Add(testMap);
        await _context.SaveChangesAsync();

        _aiNameGeneratorMock.Setup(x => x.GenerateName(It.IsAny<long>())).Returns("Attila");
        _gameStateServiceMock.Setup(x => x.BuildGameStateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameStateDto(
                new GameStateGameDto(1, 1, 1, false, "active"),
                new GameStateMapDto(103, "standard_6x8", 1, 8, 6),
                Array.Empty<ParticipantDto>(),
                Array.Empty<UnitInStateDto>(),
                Array.Empty<CityInStateDto>(),
                Array.Empty<CityTileLinkDto>(),
                Array.Empty<CityResourceDto>(),
                Array.Empty<UnitDefinitionDto>(),
                null));

        // Act
        var result = await _service.CreateGameAsync(newUserId, command, idempotencyKey, default);

        // Assert
        _idempotencyStoreMock.Verify(
            x => x.TryStoreAsync(
                CacheKeys.CreateGameIdempotency(newUserId, idempotencyKey),
                It.IsAny<GameCreatedResponse>(),
                TimeSpan.FromHours(24),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateGameAsync_WhenSeedingFails_ShouldThrowException()
    {
        // Arrange
        var newUserId = Guid.NewGuid();
        var command = new CreateGameCommand("standard_6x8", null, "Player");
        
        var testMap = new Map
        {
            Id = 104,
            Code = "standard_6x8",
            SchemaVersion = 1,
            Width = 8,
            Height = 6
        };
        _context.Maps.Add(testMap);
        await _context.SaveChangesAsync();

        _aiNameGeneratorMock.Setup(x => x.GenerateName(It.IsAny<long>())).Returns("Napoleon");
        
        // Setup seeding to fail
        _gameSeedingServiceMock
            .Setup(x => x.SeedGameEntitiesAsync(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Seeding failed"));

        // Act & Assert - Verify exception is thrown
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateGameAsync(newUserId, command, null, default));

        // Note: Transaction rollback cannot be fully tested with InMemory database provider
        // as it doesn't support real transactions. In production with a real database,
        // the transaction would roll back and no data would be persisted.
    }

    [Fact]
    public async Task VerifyGameAccessAsync_ForUserOwnedGame_ShouldReturnTrue()
    {
        // Arrange - using seeded game with ID 1

        // Act
        var result = await _service.VerifyGameAccessAsync(_testUserId, 1, default);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyGameAccessAsync_ForOtherUsersGame_ShouldReturnFalse()
    {
        // Arrange - game ID 4 belongs to a different user

        // Act
        var result = await _service.VerifyGameAccessAsync(_testUserId, 4, default);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyGameAccessAsync_ForNonExistentGame_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentGameId = 999L;

        // Act
        var result = await _service.VerifyGameAccessAsync(_testUserId, nonExistentGameId, default);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetGameDetailAsync_ForUserOwnedGame_ShouldReturnDetail()
    {
        // Arrange - game ID 1 belongs to _testUserId (seeded)

        // Act
        var detail = await _service.GetGameDetailAsync(_testUserId, 1, default);

        // Assert
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(1);
        detail.UserId.Should().Be(_testUserId);
        detail.MapId.Should().Be(1);
        detail.TurnNo.Should().Be(1);
        detail.Status.Should().Be("active");
    }

    [Fact]
    public async Task GetGameDetailAsync_ForOtherUsersGame_ShouldReturnNull()
    {
        // Arrange - game ID 4 belongs to a different user (seeded)

        // Act
        var detail = await _service.GetGameDetailAsync(_testUserId, 4, default);

        // Assert
        detail.Should().BeNull();
    }

    [Fact]
    public async Task GetGameDetailAsync_ForNonExistentGame_ShouldReturnNull()
    {
        // Arrange
        var nonExistent = 12345L;

        // Act
        var detail = await _service.GetGameDetailAsync(_testUserId, nonExistent, default);

        // Assert
        detail.Should().BeNull();
    }

    #region DeleteGameAsync Tests

    [Fact(Skip = "ExecuteSqlInterpolatedAsync not supported by InMemoryDatabase. Test with real database in integration tests.")]
    public async Task DeleteGameAsync_WithValidGame_ShouldReturnTrueAndDeleteAllRelatedEntities()
    {
        // Note: This test is skipped because ExecuteSqlInterpolatedAsync is not supported
        // by the InMemoryDatabase provider. The manual cascade delete logic using raw SQL
        // can only be properly tested with a real relational database (e.g., PostgreSQL).
        //
        // Integration tests with a real database connection should verify:
        // - Game is deleted
        // - All related entities are cascade deleted (participants, units, cities, etc.)
        //
        // For unit testing, we rely on the simpler tests below that verify
        // ownership checks and idempotency behavior.

        await Task.CompletedTask; // Suppress async warning
    }

    [Fact]
    public async Task DeleteGameAsync_WithNonExistentGame_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentGameId = 999L;

        // Act
        var result = await _service.DeleteGameAsync(_testUserId, nonExistentGameId, null, default);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteGameAsync_WithGameOwnedByDifferentUser_ShouldReturnFalse()
    {
        // Arrange - Game ID 4 belongs to a different user (seeded)
        var gameId = 4L;

        // Act
        var result = await _service.DeleteGameAsync(_testUserId, gameId, null, default);

        // Assert
        result.Should().BeFalse();

        // Verify game still exists (not deleted)
        var game = await _context.Games.FindAsync(gameId);
        game.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteGameAsync_WithIdempotencyKey_ShouldCheckCache()
    {
        // Arrange
        var gameId = 1L;
        var idempotencyKey = "delete-key-123";
        var cachedKey = CacheKeys.DeleteGameIdempotency(_testUserId, idempotencyKey);

        _idempotencyStoreMock
            .Setup(x => x.TryGetAsync<string>(cachedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync("deleted");

        // Act
        var result = await _service.DeleteGameAsync(_testUserId, gameId, idempotencyKey, default);

        // Assert
        result.Should().BeTrue();

        // Verify cache was checked
        _idempotencyStoreMock.Verify(
            x => x.TryGetAsync<string>(cachedKey, It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify game was NOT deleted (returned from cache)
        var game = await _context.Games.FindAsync(gameId);
        game.Should().NotBeNull();
    }

    [Fact(Skip = "ExecuteSqlInterpolatedAsync not supported by InMemoryDatabase. Test with real database in integration tests.")]
    public async Task DeleteGameAsync_WithIdempotencyKey_ShouldStoreSuccessInCache()
    {
        // Note: Skipped for same reason as DeleteGameAsync_WithValidGame_ShouldReturnTrueAndDeleteAllRelatedEntities
        // The actual deletion uses ExecuteSqlInterpolatedAsync which requires a relational database.
        
        await Task.CompletedTask; // Suppress async warning
    }

    [Fact]
    public async Task DeleteGameAsync_WithIdempotencyKeyForNotFound_ShouldStoreNotFoundInCache()
    {
        // Arrange
        var nonExistentGameId = 999L;
        var idempotencyKey = "delete-key-789";
        var cachedKey = CacheKeys.DeleteGameIdempotency(_testUserId, idempotencyKey);

        _idempotencyStoreMock
            .Setup(x => x.TryGetAsync<string>(cachedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.DeleteGameAsync(_testUserId, nonExistentGameId, idempotencyKey, default);

        // Assert
        result.Should().BeFalse();

        // Verify not-found was stored in cache
        _idempotencyStoreMock.Verify(
            x => x.TryStoreAsync(
                cachedKey,
                "not_found",
                TimeSpan.FromHours(24),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteGameAsync_WithIdempotencyKeyCachedAsNotFound_ShouldReturnFalse()
    {
        // Arrange
        var gameId = 1L;
        var idempotencyKey = "delete-key-not-found";
        var cachedKey = CacheKeys.DeleteGameIdempotency(_testUserId, idempotencyKey);

        _idempotencyStoreMock
            .Setup(x => x.TryGetAsync<string>(cachedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync("not_found");

        // Act
        var result = await _service.DeleteGameAsync(_testUserId, gameId, idempotencyKey, default);

        // Assert
        result.Should().BeFalse();

        // Verify game still exists (returned from cache)
        var game = await _context.Games.FindAsync(gameId);
        game.Should().NotBeNull();
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

