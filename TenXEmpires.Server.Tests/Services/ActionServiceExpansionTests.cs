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
using TenXEmpires.Server.Domain.Constants;

namespace TenXEmpires.Server.Tests.Services;

public class ActionServiceExpansionTests : IDisposable
{
    private readonly TenXDbContext _context;
    private readonly ActionService _service;
    private readonly Mock<IGameStateService> _gameStateServiceMock;
    private readonly Mock<IIdempotencyStore> _idempotencyStoreMock;
    private readonly Mock<ILogger<ActionService>> _loggerMock;
    private readonly Guid _testUserId;
    private readonly long _testGameId = 1L;
    private readonly long _humanParticipantId = 1L;
    private readonly long _testMapId = 1L;
    private readonly long _testCityId = 1L;

    public ActionServiceExpansionTests()
    {
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

        SeedTestData();
    }

    private void SeedTestData()
    {
        var now = DateTimeOffset.UtcNow;

        // Map
        var map = new Map { Id = _testMapId, Code = "test_5x5", SchemaVersion = 1, Width = 5, Height = 5 };
        _context.Maps.Add(map);

        // Map Tiles (5x5 grid)
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                _context.MapTiles.Add(new MapTile
                {
                    Id = (row * 5) + col + 1,
                    MapId = _testMapId,
                    Row = row,
                    Col = col,
                    Terrain = "plains",
                    ResourceType = null,
                    ResourceAmount = 0
                });
            }
        }

        // Game
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

        // Participant
        var humanParticipant = new Participant
        {
            Id = _humanParticipantId,
            GameId = _testGameId,
            Kind = "human",
            UserId = _testUserId,
            DisplayName = "Player",
            IsEliminated = false
        };
        _context.Participants.Add(humanParticipant);
        game.ActiveParticipantId = _humanParticipantId;

        // City at (2,2) -> Tile ID: 2*5 + 2 + 1 = 13
        var cityTile = _context.MapTiles.Local.First(t => t.Row == 2 && t.Col == 2);
        var city = new City
        {
            Id = _testCityId,
            GameId = _testGameId,
            ParticipantId = _humanParticipantId,
            TileId = cityTile.Id,
            Hp = 100,
            MaxHp = 100,
            HasActedThisTurn = false
        };
        _context.Cities.Add(city);

        // City Resources (Seed with enough wheat for base cost)
        _context.CityResources.Add(new CityResource
        {
            Id = 1,
            CityId = _testCityId,
            ResourceType = ResourceTypes.Wheat,
            Amount = 50 // Base cost is 20
        });

        // Initial City Tiles (Center + 6 Neighbors for standard, but let's start with just center for simplicity or standard setup)
        // Center (2,2)
        _context.CityTiles.Add(new CityTile { GameId = _testGameId, CityId = _testCityId, TileId = cityTile.Id });
        
        // Let's add neighbors to make it "InitialTilesCount" compliant if needed, or test cost logic dynamically.
        // Standard setup usually gives 7 tiles.
        // Let's add (2,1), (2,3), (1,1), (1,2), (3,1), (3,2) - approx neighbors
        // Neighbors of (2,2) odd-r:
        // (2,1) - Left
        // (2,3) - Right
        // (1,2) - Top Right (Even row above) - wait, row 1 is odd? 0 is even. 1 is odd. 2 is even.
        // Odd-r offset:
        // Even row (2): (2,1), (2,3), (1,1), (1,2), (3,1), (3,2)
        // Top-Left: (1,1), Top-Right: (1,2)
        // Bottom-Left: (3,1), Bottom-Right: (3,2)
        
        // Adding these to CityTiles
        var neighborCoords = new[] { (2,1), (2,3), (1,1), (1,2), (3,1), (3,2) };
        foreach (var (r, c) in neighborCoords)
        {
            var t = _context.MapTiles.Local.First(ti => ti.Row == r && ti.Col == c);
            _context.CityTiles.Add(new CityTile { GameId = _testGameId, CityId = _testCityId, TileId = t.Id });
        }
        // Total 7 tiles.

        _context.SaveChanges();
    }

    [Fact]
    public async Task ExpandTerritoryAsync_ValidExpansion_ShouldSucceed()
    {
        // Arrange
        // Current tiles: (2,2) and immediate neighbors.
        // Expand to a tile adjacent to one of the neighbors, e.g., (2,0) which is left of (2,1)
        var targetTile = _context.MapTiles.Local.First(t => t.Row == 2 && t.Col == 0);
        var command = new ExpandTerritoryCommand(_testCityId, targetTile.Id);

        var expectedGameState = new GameStateDto(
            new GameStateGameDto(1, 1, 1, false, "active"),
            new GameStateMapDto(1, "test_5x5", 1, 5, 5),
            new List<ParticipantDto>(),
            new List<UnitInStateDto>(),
            new List<CityInStateDto>(),
            new List<CityTileLinkDto>(),
            new List<CityResourceDto>(),
            new List<GameTileStateDto>(),
            new List<UnitDefinitionDto>(),
            null);

        _gameStateServiceMock.Setup(x => x.BuildGameStateAsync(_testGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedGameState);

        // Act
        var result = await _service.ExpandTerritoryAsync(_testUserId, _testGameId, command, null);

        // Assert
        result.Should().NotBeNull();
        
        // Verify DB updates
        var city = await _context.Cities.Include(c => c.CityResources).FirstAsync(c => c.Id == _testCityId);
        city.HasActedThisTurn.Should().BeTrue();
        
        // Cost: Base 20 + (7-7)*10 = 20. Initial 50. Remaining 30.
        var wheat = city.CityResources.First(r => r.ResourceType == ResourceTypes.Wheat);
        wheat.Amount.Should().Be(30);

        var newTile = await _context.CityTiles.FirstOrDefaultAsync(ct => ct.CityId == _testCityId && ct.TileId == targetTile.Id);
        newTile.Should().NotBeNull();
    }

    [Fact]
    public async Task ExpandTerritoryAsync_CityAlreadyActed_ShouldThrow()
    {
        // Arrange
        var city = await _context.Cities.FindAsync(_testCityId);
        city!.HasActedThisTurn = true;
        await _context.SaveChangesAsync();

        var targetTile = _context.MapTiles.Local.First(t => t.Row == 2 && t.Col == 0);
        var command = new ExpandTerritoryCommand(_testCityId, targetTile.Id);

        // Act & Assert
        var act = async () => await _service.ExpandTerritoryAsync(_testUserId, _testGameId, command, null);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*CITY_ALREADY_ACTED*");
    }

    [Fact]
    public async Task ExpandTerritoryAsync_InsufficientResources_ShouldThrow()
    {
        // Arrange
        var wheat = await _context.CityResources.FirstAsync(r => r.CityId == _testCityId && r.ResourceType == ResourceTypes.Wheat);
        wheat.Amount = 10; // Cost is 20
        await _context.SaveChangesAsync();

        var targetTile = _context.MapTiles.Local.First(t => t.Row == 2 && t.Col == 0);
        var command = new ExpandTerritoryCommand(_testCityId, targetTile.Id);

        // Act & Assert
        var act = async () => await _service.ExpandTerritoryAsync(_testUserId, _testGameId, command, null);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*INSUFFICIENT_RESOURCES*");
    }

    [Fact]
    public async Task ExpandTerritoryAsync_NonAdjacentTile_ShouldThrow()
    {
        // Arrange
        // Pick a tile far away, e.g., (4,4)
        var targetTile = _context.MapTiles.Local.First(t => t.Row == 4 && t.Col == 4);
        var command = new ExpandTerritoryCommand(_testCityId, targetTile.Id);

        // Act & Assert
        var act = async () => await _service.ExpandTerritoryAsync(_testUserId, _testGameId, command, null);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*TILE_NOT_ADJACENT*");
    }

    [Fact]
    public async Task ExpandTerritoryAsync_AlreadyOwnedTile_ShouldThrow()
    {
        // Arrange
        // Try to expand to (2,1) which is already owned
        var targetTile = _context.MapTiles.Local.First(t => t.Row == 2 && t.Col == 1);
        var command = new ExpandTerritoryCommand(_testCityId, targetTile.Id);

        // Act & Assert
        var act = async () => await _service.ExpandTerritoryAsync(_testUserId, _testGameId, command, null);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*TILE_ALREADY_OWNED*");
    }

    [Fact]
    public async Task ExpandTerritoryAsync_WaterTile_ShouldThrow()
    {
        // Arrange
        // Set a valid adjacent tile (2,0) to Water
        var targetTile = await _context.MapTiles.FirstAsync(t => t.Row == 2 && t.Col == 0);
        targetTile.Terrain = "water";
        await _context.SaveChangesAsync();

        var command = new ExpandTerritoryCommand(_testCityId, targetTile.Id);

        // Act & Assert
        var act = async () => await _service.ExpandTerritoryAsync(_testUserId, _testGameId, command, null);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*INVALID_TERRAIN*");
    }

    [Fact]
    public async Task ExpandTerritoryAsync_EnemyOwnedTile_ShouldThrow()
    {
        // Arrange
        // Create enemy participant
        var enemyId = 2L;
        _context.Participants.Add(new Participant { Id = enemyId, GameId = _testGameId, Kind = "human", UserId = Guid.NewGuid(), DisplayName = "Enemy", IsEliminated = false });
        
        // Create enemy city
        var enemyCity = new City { Id = 2, GameId = _testGameId, ParticipantId = enemyId, TileId = 99, Hp = 100, MaxHp = 100 };
        _context.Cities.Add(enemyCity);

        // Give enemy a tile adjacent to us, e.g., (2,0)
        var targetTile = await _context.MapTiles.FirstAsync(t => t.Row == 2 && t.Col == 0);
        _context.CityTiles.Add(new CityTile { GameId = _testGameId, CityId = enemyCity.Id, TileId = targetTile.Id });
        await _context.SaveChangesAsync();

        var command = new ExpandTerritoryCommand(_testCityId, targetTile.Id);

        // Act & Assert
        var act = async () => await _service.ExpandTerritoryAsync(_testUserId, _testGameId, command, null);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*TILE_OWNED_BY_ENEMY*");
    }

    [Fact]
    public async Task ExpandTerritoryAsync_EnemyUnitOccupied_ShouldThrow()
    {
        // Arrange
        // Create enemy unit on valid adjacent tile (2,0)
        var enemyId = 2L;
        var targetTile = await _context.MapTiles.FirstAsync(t => t.Row == 2 && t.Col == 0);
        
        _context.Units.Add(new Unit 
        { 
            Id = 100, 
            GameId = _testGameId, 
            ParticipantId = enemyId, 
            TileId = targetTile.Id, 
            TypeId = 1, 
            Hp = 100, 
            HasActed = false, 
            UpdatedAt = DateTimeOffset.UtcNow 
        });
        await _context.SaveChangesAsync();

        var command = new ExpandTerritoryCommand(_testCityId, targetTile.Id);

        // Act & Assert
        var act = async () => await _service.ExpandTerritoryAsync(_testUserId, _testGameId, command, null);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*TILE_OCCUPIED_BY_ENEMY*");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

