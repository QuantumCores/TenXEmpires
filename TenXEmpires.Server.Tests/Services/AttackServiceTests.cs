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

public class AttackServiceTests : IDisposable
{
    private readonly TenXDbContext _context;
    private readonly ActionService _service;
    private readonly Mock<IGameStateService> _gameStateServiceMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyStoreMock = new();
    private readonly Mock<ILogger<ActionService>> _loggerMock = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly long _gameId = 1L;
    private readonly long _humanPid = 1L;
    private readonly long _aiPid = 2L;

    public AttackServiceTests()
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new TenXDbContext(options);
        _service = new ActionService(_context, _gameStateServiceMock.Object, _idempotencyStoreMock.Object, _loggerMock.Object);

        Seed();
    }

    private void Seed()
    {
        var now = DateTimeOffset.UtcNow;

        var map = new Map { Id = 1, Code = "test_4x4", SchemaVersion = 1, Width = 4, Height = 4 };
        _context.Maps.Add(map);
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                _context.MapTiles.Add(new MapTile
                {
                    Id = r * 4 + c + 1,
                    MapId = 1,
                    Row = r,
                    Col = c,
                    Terrain = "plains"
                });
            }
        }

        var game = new Game
        {
            Id = _gameId,
            UserId = _userId,
            MapId = 1,
            MapSchemaVersion = 1,
            TurnNo = 1,
            ActiveParticipantId = _humanPid,
            TurnInProgress = false,
            Status = "active",
            RngSeed = 123,
            StartedAt = now,
            Settings = "{}"
        };
        _context.Games.Add(game);

        _context.Participants.AddRange(
            new Participant { Id = _humanPid, GameId = _gameId, Kind = "human", UserId = _userId, DisplayName = "P1" },
            new Participant { Id = _aiPid, GameId = _gameId, Kind = "ai", UserId = null, DisplayName = "AI" }
        );

        var melee = new UnitDefinition { Id = 1, Code = "warrior", IsRanged = false, Attack = 20, Defence = 10, RangeMin = 0, RangeMax = 0, MovePoints = 2, Health = 100 };
        var ranged = new UnitDefinition { Id = 2, Code = "slinger", IsRanged = true, Attack = 15, Defence = 8, RangeMin = 1, RangeMax = 2, MovePoints = 2, Health = 80 };
        _context.UnitDefinitions.AddRange(melee, ranged);

        // Default units used by tests (will be adjusted per test as needed)
        _context.Units.AddRange(
            new Unit { Id = 101, GameId = _gameId, ParticipantId = _humanPid, TypeId = 1, TileId = 1, Hp = 100, HasActed = false, UpdatedAt = now }, // (0,0) warrior
            new Unit { Id = 201, GameId = _gameId, ParticipantId = _aiPid, TypeId = 1, TileId = 2, Hp = 100, HasActed = false, UpdatedAt = now }     // (0,1) warrior
        );

        _context.SaveChanges();
    }

    [Fact]
    public async Task AttackAsync_MeleeVsMelee_Adjacent_ShouldApplyCounterattackAndMarkActed()
    {
        // Arrange
        var cmd = new AttackUnitCommand(AttackerUnitId: 101, TargetUnitId: 201);

        _gameStateServiceMock
            .Setup(x => x.BuildGameStateAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameStateDto(
                new GameStateGameDto(_gameId, 1, _humanPid, false, "active"),
                new GameStateMapDto(1, "test_4x4", 1, 4, 4),
                new List<ParticipantDto>(), new List<UnitInStateDto>(), new List<CityInStateDto>(),
                new List<CityTileLinkDto>(), new List<CityResourceDto>(), new List<GameTileStateDto>(), new List<UnitDefinitionDto>(), null));

        _idempotencyStoreMock.Setup(x => x.TryGetAsync<ActionStateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionStateResponse?)null);

        // Act
        var result = await _service.AttackAsync(_userId, _gameId, cmd, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var attacker = await _context.Units.Include(u => u.Type).FirstOrDefaultAsync(u => u.Id == 101);
        var defender = await _context.Units.Include(u => u.Type).FirstOrDefaultAsync(u => u.Id == 201);

        attacker.Should().NotBeNull();
        defender.Should().NotBeNull();

        // Damage formula: (20^2)/(2*10) = 20
        attacker!.Hp.Should().Be(87); // counter scaled by defender HP ratio -> 13 dmg
        attacker.HasActed.Should().BeTrue();
        defender!.Hp.Should().Be(80); // took 20
    }

    [Fact]
    public async Task AttackAsync_RangedAttacker_NoCounterattack()
    {
        // Arrange: replace human attacker with ranged at (0,0), target AI warrior at (0,2) distance 2
        var now = DateTimeOffset.UtcNow;

        var att = await _context.Units.FindAsync(101L);
        if (att != null) _context.Units.Remove(att);
        _context.Units.Add(new Unit { Id = 102, GameId = _gameId, ParticipantId = _humanPid, TypeId = 2, TileId = 1, Hp = 80, HasActed = false, UpdatedAt = now }); // slinger

        var tgt = await _context.Units.FindAsync(201L);
        if (tgt != null)
        {
            // Move AI warrior to (0,2) => tileId 3
            tgt.TileId = 3;
            tgt.Hp = 100;
            tgt.HasActed = false;
            _context.Units.Update(tgt);
        }
        await _context.SaveChangesAsync();

        var cmd = new AttackUnitCommand(AttackerUnitId: 102, TargetUnitId: 201);

        _gameStateServiceMock
            .Setup(x => x.BuildGameStateAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameStateDto(
                new GameStateGameDto(_gameId, 1, _humanPid, false, "active"),
                new GameStateMapDto(1, "test_4x4", 1, 4, 4),
                new List<ParticipantDto>(), new List<UnitInStateDto>(), new List<CityInStateDto>(),
                new List<CityTileLinkDto>(), new List<CityResourceDto>(), new List<GameTileStateDto>(), new List<UnitDefinitionDto>(), null));

        _idempotencyStoreMock.Setup(x => x.TryGetAsync<ActionStateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionStateResponse?)null);

        // Act
        var result = await _service.AttackAsync(_userId, _gameId, cmd, null, CancellationToken.None);

        // Assert: damage = (15^2)/(2*10)=11.25 => 11, no counter
        var attacker = await _context.Units.Include(u => u.Type).FirstOrDefaultAsync(u => u.Id == 102);
        var defender = await _context.Units.Include(u => u.Type).FirstOrDefaultAsync(u => u.Id == 201);
        attacker.Should().NotBeNull();
        defender.Should().NotBeNull();

        attacker!.Hp.Should().Be(80); // unchanged by counter
        attacker.HasActed.Should().BeTrue();
        defender!.Hp.Should().Be(89); // 100 - 11
    }

    [Fact]
    public async Task AttackAsync_OutOfRange_ShouldThrow()
    {
        // Arrange: place target far away (row3,col3 => tileId 16); attacker melee at (0,0)
        var tgt = await _context.Units.FindAsync(201L);
        tgt!.TileId = 16; // (3,3)
        await _context.SaveChangesAsync();

        var cmd = new AttackUnitCommand(AttackerUnitId: 101, TargetUnitId: 201);

        // Act
        var act = async () => await _service.AttackAsync(_userId, _gameId, cmd, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*OUT_OF_RANGE*");
    }

    [Fact]
    public async Task AttackAsync_WithIdempotencyKey_ShouldReturnCachedResponse()
    {
        // Arrange
        var key = "attack-key-123";
        var cmd = new AttackUnitCommand(101, 201);

        var cachedState = new GameStateDto(
            new GameStateGameDto(_gameId, 1, _humanPid, false, "active"),
            new GameStateMapDto(1, "test_4x4", 1, 4, 4),
            new List<ParticipantDto>(), new List<UnitInStateDto>(), new List<CityInStateDto>(),
            new List<CityTileLinkDto>(), new List<CityResourceDto>(), new List<GameTileStateDto>(), new List<UnitDefinitionDto>(), null);
        var cachedResponse = new ActionStateResponse(cachedState);

        _idempotencyStoreMock
            .Setup(x => x.TryGetAsync<ActionStateResponse>($"attack-unit:{_gameId}:{key}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        // Act
        var result = await _service.AttackAsync(_userId, _gameId, cmd, key, CancellationToken.None);

        // Assert
        result.Should().Be(cachedResponse);

        // Verify no state rebuild was called
        _gameStateServiceMock.Verify(x => x.BuildGameStateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AttackAsync_SuccessWithIdempotencyKey_ShouldStoreInCache()
    {
        // Arrange
        var key = "attack-key-456";
        var cmd = new AttackUnitCommand(101, 201);

        _idempotencyStoreMock
            .Setup(x => x.TryGetAsync<ActionStateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionStateResponse?)null);

        _gameStateServiceMock
            .Setup(x => x.BuildGameStateAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameStateDto(
                new GameStateGameDto(_gameId, 1, _humanPid, false, "active"),
                new GameStateMapDto(1, "test_4x4", 1, 4, 4),
                new List<ParticipantDto>(), new List<UnitInStateDto>(), new List<CityInStateDto>(),
                new List<CityTileLinkDto>(), new List<CityResourceDto>(), new List<GameTileStateDto>(), new List<UnitDefinitionDto>(), null));

        _idempotencyStoreMock
            .Setup(x => x.TryStoreAsync($"attack-unit:{_gameId}:{key}", It.IsAny<ActionStateResponse>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.AttackAsync(_userId, _gameId, cmd, key, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _idempotencyStoreMock.Verify(x => x.TryStoreAsync($"attack-unit:{_gameId}:{key}", It.IsAny<ActionStateResponse>(), TimeSpan.FromHours(1), It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
