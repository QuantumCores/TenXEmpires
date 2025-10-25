using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Services;

namespace TenXEmpires.Server.Tests.Services;

public class ActionServiceTests : IDisposable
{
    private readonly TenXDbContext _context;
    private readonly ActionService _service;
    private readonly Mock<IGameStateService> _gameStateServiceMock;
    private readonly Mock<IIdempotencyStore> _idempotencyStoreMock;
    private readonly Mock<ILogger<ActionService>> _loggerMock;
    private readonly Guid _testUserId;
    private readonly long _testGameId = 1L;
    private readonly long _humanParticipantId = 1L;
    private readonly long _aiParticipantId = 2L;
    private readonly long _testMapId = 1L;

    public ActionServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new TenXDbContext(options);
        _gameStateServiceMock = new Mock<IGameStateService>();
        _idempotencyStoreMock = new Mock<IIdempotencyStore>();
        _loggerMock = new Mock<ILogger<ActionService>>();

        _service = new ActionService(
            _context,
            _gameStateServiceMock.Object,
            _idempotencyStoreMock.Object,
            _loggerMock.Object);

        _testUserId = Guid.NewGuid();

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var now = DateTimeOffset.UtcNow;

        // Create a test map
        var map = new Map
        {
            Id = _testMapId,
            Code = "test_4x4",
            SchemaVersion = 1,
            Width = 4,
            Height = 4,
            GeneratedAt = now
        };
        _context.Maps.Add(map);

        // Create map tiles (4x4 grid)
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                _context.MapTiles.Add(new MapTile
                {
                    Id = (row * 4) + col + 1,
                    MapId = _testMapId,
                    Row = row,
                    Col = col,
                    Terrain = "plains",
                    ResourceType = null,
                    ResourceAmount = 0
                });
            }
        }

        // Create a test game
        var game = new Game
        {
            Id = _testGameId,
            UserId = _testUserId,
            MapId = _testMapId,
            MapSchemaVersion = 1,
            TurnNo = 1,
            Status = "active",
            StartedAt = now,
            TurnInProgress = false,
            RngSeed = 12345,
            Settings = "{}"
        };
        _context.Games.Add(game);

        // Create participants
        var humanParticipant = new Participant
        {
            Id = _humanParticipantId,
            GameId = _testGameId,
            Kind = "human",
            UserId = _testUserId,
            DisplayName = "Player",
            IsEliminated = false
        };

        var aiParticipant = new Participant
        {
            Id = _aiParticipantId,
            GameId = _testGameId,
            Kind = "ai",
            UserId = null,
            DisplayName = "AI",
            IsEliminated = false
        };

        _context.Participants.AddRange(humanParticipant, aiParticipant);

        // Set active participant to human
        game.ActiveParticipantId = _humanParticipantId;

        // Create unit definitions
        var warriorDef = new UnitDefinition
        {
            Id = 1,
            Code = "warrior",
            IsRanged = false,
            Attack = 20,
            Defence = 10,
            RangeMin = 0,
            RangeMax = 0,
            MovePoints = 2,
            Health = 100
        };
        _context.UnitDefinitions.Add(warriorDef);

        // Create units
        // Human warrior at (0, 0)
        _context.Units.Add(new Unit
        {
            Id = 1,
            GameId = _testGameId,
            ParticipantId = _humanParticipantId,
            TypeId = 1,
            TileId = 1, // Row 0, Col 0
            Hp = 100,
            HasActed = false,
            UpdatedAt = now
        });

        // Human warrior at (1, 1) - to test blocking
        _context.Units.Add(new Unit
        {
            Id = 2,
            GameId = _testGameId,
            ParticipantId = _humanParticipantId,
            TypeId = 1,
            TileId = 6, // Row 1, Col 1
            Hp = 100,
            HasActed = false,
            UpdatedAt = now
        });

        // AI warrior at (3, 3)
        _context.Units.Add(new Unit
        {
            Id = 3,
            GameId = _testGameId,
            ParticipantId = _aiParticipantId,
            TypeId = 1,
            TileId = 16, // Row 3, Col 3
            Hp = 100,
            HasActed = false,
            UpdatedAt = now
        });

        _context.SaveChanges();
    }

    [Fact]
    public async Task MoveUnitAsync_ValidMove_ShouldSucceed()
    {
        // Arrange
        // On a hexagonal grid (odd-r), from (0,0) neighbors are at specific positions
        // Move from (0,0) to (0,1) - this is a valid hexagonal neighbor for row 0 (even row)
        var command = new MoveUnitCommand(1, new GridPosition(0, 1));

        var expectedGameState = new GameStateDto(
            new GameStateGameDto(1, 1, 1, false, "active"),
            new GameStateMapDto(1, "test_4x4", 1, 4, 4),
            new List<ParticipantDto>(),
            new List<UnitInStateDto>(),
            new List<CityInStateDto>(),
            new List<CityTileLinkDto>(),
            new List<CityResourceDto>(),
            new List<UnitDefinitionDto>(),
            null);

        _gameStateServiceMock
            .Setup(x => x.BuildGameStateAsync(_testGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedGameState);

        _idempotencyStoreMock
            .Setup(x => x.TryGetAsync<ActionStateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionStateResponse?)null);

        // Act
        var result = await _service.MoveUnitAsync(_testUserId, _testGameId, command, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(expectedGameState);

        // Verify unit was updated
        var updatedUnit = await _context.Units.FindAsync(1L);
        updatedUnit.Should().NotBeNull();
        updatedUnit!.TileId.Should().Be(2); // Row 0, Col 1 (tile ID = row*4 + col + 1)
        updatedUnit.HasActed.Should().BeTrue();

        // Verify game state was built
        _gameStateServiceMock.Verify(
            x => x.BuildGameStateAsync(_testGameId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MoveUnitAsync_NotPlayerTurn_ShouldThrowInvalidOperationException()
    {
        // Arrange
        // Set active participant to AI
        var game = await _context.Games.FindAsync(_testGameId);
        game!.ActiveParticipantId = _aiParticipantId;
        await _context.SaveChangesAsync();

        var command = new MoveUnitCommand(1, new GridPosition(0, 1));

        // Act & Assert
        var act = async () => await _service.MoveUnitAsync(_testUserId, _testGameId, command, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*NOT_PLAYER_TURN*");
    }

    [Fact]
    public async Task MoveUnitAsync_UnitAlreadyActed_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var unit = await _context.Units.FindAsync(1L);
        unit!.HasActed = true;
        await _context.SaveChangesAsync();

        var command = new MoveUnitCommand(1, new GridPosition(0, 1));

        // Act & Assert
        var act = async () => await _service.MoveUnitAsync(_testUserId, _testGameId, command, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*NO_ACTIONS_LEFT*");
    }

    [Fact]
    public async Task MoveUnitAsync_DestinationOccupied_ShouldThrowInvalidOperationException()
    {
        // Arrange
        // Try to move unit 1 from (0,0) to (1,1) where unit 2 is located
        var command = new MoveUnitCommand(1, new GridPosition(1, 1));

        // Act & Assert
        var act = async () => await _service.MoveUnitAsync(_testUserId, _testGameId, command, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ONE_UNIT_PER_TILE*");
    }

    [Fact]
    public async Task MoveUnitAsync_OutOfRange_ShouldThrowArgumentException()
    {
        // Arrange
        // Try to move unit 1 from (0,0) to (3,0) - too far (warrior has 2 move points) and tile is empty
        var command = new MoveUnitCommand(1, new GridPosition(3, 0));

        // Act & Assert
        var act = async () => await _service.MoveUnitAsync(_testUserId, _testGameId, command, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ILLEGAL_MOVE*");
    }

    [Fact]
    public async Task MoveUnitAsync_OutOfBounds_ShouldThrowArgumentException()
    {
        // Arrange
        // Try to move unit 1 from (0,0) to (5,5) - out of map bounds (map is 4x4)
        var command = new MoveUnitCommand(1, new GridPosition(5, 5));

        // Act & Assert
        var act = async () => await _service.MoveUnitAsync(_testUserId, _testGameId, command, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*out of map bounds*");
    }

    [Fact]
    public async Task MoveUnitAsync_UnitNotFound_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var command = new MoveUnitCommand(999, new GridPosition(0, 1)); // Non-existent unit

        // Act & Assert
        var act = async () => await _service.MoveUnitAsync(_testUserId, _testGameId, command, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task MoveUnitAsync_UnitBelongsToOtherParticipant_ShouldThrowInvalidOperationException()
    {
        // Arrange
        // Try to move unit 3 which belongs to AI participant
        var command = new MoveUnitCommand(3, new GridPosition(3, 2));

        // Act & Assert
        var act = async () => await _service.MoveUnitAsync(_testUserId, _testGameId, command, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not belong to the active participant*");
    }

    [Fact]
    public async Task MoveUnitAsync_GameNotFound_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var command = new MoveUnitCommand(1, new GridPosition(0, 1));
        var wrongUserId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await _service.MoveUnitAsync(wrongUserId, _testGameId, command, null, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not found or you don't have access*");
    }

    [Fact]
    public async Task MoveUnitAsync_WithIdempotencyKey_ShouldReturnCachedResponse()
    {
        // Arrange
        var command = new MoveUnitCommand(1, new GridPosition(0, 1));
        var idempotencyKey = "test-key-123";

        var cachedGameState = new GameStateDto(
            new GameStateGameDto(1, 1, 1, false, "active"),
            new GameStateMapDto(1, "test_4x4", 1, 4, 4),
            new List<ParticipantDto>(),
            new List<UnitInStateDto>(),
            new List<CityInStateDto>(),
            new List<CityTileLinkDto>(),
            new List<CityResourceDto>(),
            new List<UnitDefinitionDto>(),
            null);

        var cachedResponse = new ActionStateResponse(cachedGameState);

        _idempotencyStoreMock
            .Setup(x => x.TryGetAsync<ActionStateResponse>(
                $"move-unit:{_testGameId}:{idempotencyKey}",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        // Act
        var result = await _service.MoveUnitAsync(_testUserId, _testGameId, command, idempotencyKey, CancellationToken.None);

        // Assert
        result.Should().Be(cachedResponse);

        // Verify unit was NOT updated (cached response used)
        var unit = await _context.Units.FindAsync(1L);
        unit!.HasActed.Should().BeFalse();
        unit.TileId.Should().Be(1); // Still at original position

        // Verify game state service was NOT called
        _gameStateServiceMock.Verify(
            x => x.BuildGameStateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MoveUnitAsync_SuccessWithIdempotencyKey_ShouldStoreInCache()
    {
        // Arrange
        var command = new MoveUnitCommand(1, new GridPosition(0, 1));
        var idempotencyKey = "test-key-456";

        var expectedGameState = new GameStateDto(
            new GameStateGameDto(1, 1, 1, false, "active"),
            new GameStateMapDto(1, "test_4x4", 1, 4, 4),
            new List<ParticipantDto>(),
            new List<UnitInStateDto>(),
            new List<CityInStateDto>(),
            new List<CityTileLinkDto>(),
            new List<CityResourceDto>(),
            new List<UnitDefinitionDto>(),
            null);

        _gameStateServiceMock
            .Setup(x => x.BuildGameStateAsync(_testGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedGameState);

        _idempotencyStoreMock
            .Setup(x => x.TryGetAsync<ActionStateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionStateResponse?)null);

        _idempotencyStoreMock
            .Setup(x => x.TryStoreAsync(
                $"move-unit:{_testGameId}:{idempotencyKey}",
                It.IsAny<ActionStateResponse>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.MoveUnitAsync(_testUserId, _testGameId, command, idempotencyKey, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        // Verify response was stored in cache
        _idempotencyStoreMock.Verify(
            x => x.TryStoreAsync(
                $"move-unit:{_testGameId}:{idempotencyKey}",
                It.IsAny<ActionStateResponse>(),
                TimeSpan.FromHours(1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MoveUnitAsync_TurnInProgress_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var game = await _context.Games.FindAsync(_testGameId);
        game!.TurnInProgress = true;
        await _context.SaveChangesAsync();

        var command = new MoveUnitCommand(1, new GridPosition(0, 1));

        // Act & Assert
        var act = async () => await _service.MoveUnitAsync(_testUserId, _testGameId, command, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in progress*");
    }

    [Fact]
    public async Task MoveUnitAsync_MoveTwoSteps_ShouldSucceed()
    {
        // Arrange
        // Move unit 1 from (0,0) to (0,2) - this is 2 hexagonal steps away in a straight line
        // On hexagonal grid (odd-r), from (0,0) we can reach (0,2) in 2 moves
        var command = new MoveUnitCommand(1, new GridPosition(0, 2));

        var expectedGameState = new GameStateDto(
            new GameStateGameDto(1, 1, 1, false, "active"),
            new GameStateMapDto(1, "test_4x4", 1, 4, 4),
            new List<ParticipantDto>(),
            new List<UnitInStateDto>(),
            new List<CityInStateDto>(),
            new List<CityTileLinkDto>(),
            new List<CityResourceDto>(),
            new List<UnitDefinitionDto>(),
            null);

        _gameStateServiceMock
            .Setup(x => x.BuildGameStateAsync(_testGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedGameState);

        _idempotencyStoreMock
            .Setup(x => x.TryGetAsync<ActionStateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionStateResponse?)null);

        // Act
        var result = await _service.MoveUnitAsync(_testUserId, _testGameId, command, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        // Verify unit was updated to correct position
        var updatedUnit = await _context.Units.FindAsync(1L);
        updatedUnit.Should().NotBeNull();
        updatedUnit!.TileId.Should().Be(3); // Row 0, Col 2 (tile ID = 0*4 + 2 + 1 = 3)
        updatedUnit.HasActed.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

