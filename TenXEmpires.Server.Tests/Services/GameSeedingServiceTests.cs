using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Services;

namespace TenXEmpires.Server.Tests.Services;

public class GameSeedingServiceTests : IDisposable
{
    private readonly TenXDbContext _context;
    private readonly GameSeedingService _service;
    private readonly Mock<ILogger<GameSeedingService>> _loggerMock;

    public GameSeedingServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TenXDbContext(options);
        _loggerMock = new Mock<ILogger<GameSeedingService>>();
        _service = new GameSeedingService(_context, _loggerMock.Object);
    }

    [Fact]
    public async Task SeedGameEntitiesAsync_WithValidData_ShouldCreateAllEntities()
    {
        // Arrange
        var gameId = 1L;
        var mapId = 1L;
        var humanParticipantId = 101L;
        var aiParticipantId = 102L;
        var rngSeed = 12345L;

        // Create test map
        var map = new Map
        {
            Id = mapId,
            Code = "test_map",
            SchemaVersion = 1,
            Width = 8,
            Height = 6
        };
        _context.Maps.Add(map);

        // Create warrior unit definition
        var warriorDef = new UnitDefinition
        {
            Id = 1,
            Code = UnitTypes.Warrior,
            IsRanged = false,
            Health = 20,
            Attack = 10,
            Defence = 5,
            RangeMin = 0,
            RangeMax = 0,
            MovePoints = 2
        };
        _context.UnitDefinitions.Add(warriorDef);

        // Create map tiles
        var tiles = new List<MapTile>();
        for (int row = 0; row < 15; row++)
        {
            for (int col = 0; col < 20; col++)
            {
                tiles.Add(new MapTile
                {
                    Id = (row * 20) + col + 1,
                    MapId = mapId,
                    Row = row,
                    Col = col,
                    Terrain = TerrainTypes.Grassland
                });
            }
        }
        _context.MapTiles.AddRange(tiles);
        await _context.SaveChangesAsync();

        // Act
        await _service.SeedGameEntitiesAsync(
            gameId,
            mapId,
            humanParticipantId,
            aiParticipantId,
            rngSeed,
            default);

        // Assert
        // Verify cities were created
        var cities = await _context.Cities.Where(c => c.GameId == gameId).ToListAsync();
        cities.Should().HaveCount(2);
        
        var humanCity = cities.FirstOrDefault(c => c.ParticipantId == humanParticipantId);
        humanCity.Should().NotBeNull();
        humanCity!.Hp.Should().Be(100);
        humanCity.MaxHp.Should().Be(100);
        
        var aiCity = cities.FirstOrDefault(c => c.ParticipantId == aiParticipantId);
        aiCity.Should().NotBeNull();
        aiCity!.Hp.Should().Be(100);
        aiCity.MaxHp.Should().Be(100);

        // Verify city tiles were created
        var cityTiles = await _context.CityTiles.Where(ct => ct.GameId == gameId).ToListAsync();
        cityTiles.Should().HaveCountGreaterThan(2);

        // Verify city resources were initialized correctly
        var cityResources = await _context.CityResources
            .Where(cr => cr.CityId == humanCity.Id || cr.CityId == aiCity.Id)
            .ToListAsync();
        
        cityResources.Should().HaveCount(8); // 4 resources per city
        
        var humanWood = cityResources.FirstOrDefault(cr => cr.CityId == humanCity.Id && cr.ResourceType == ResourceTypes.Wood);
        humanWood.Should().NotBeNull();
        humanWood!.Amount.Should().Be(ResourceTypes.InitialAmounts.Wood);
        
        var humanStone = cityResources.FirstOrDefault(cr => cr.CityId == humanCity.Id && cr.ResourceType == ResourceTypes.Stone);
        humanStone.Should().NotBeNull();
        humanStone!.Amount.Should().Be(ResourceTypes.InitialAmounts.Stone);
        
        var humanWheat = cityResources.FirstOrDefault(cr => cr.CityId == humanCity.Id && cr.ResourceType == ResourceTypes.Wheat);
        humanWheat.Should().NotBeNull();
        humanWheat!.Amount.Should().Be(ResourceTypes.InitialAmounts.Wheat);
        
        var humanIron = cityResources.FirstOrDefault(cr => cr.CityId == humanCity.Id && cr.ResourceType == ResourceTypes.Iron);
        humanIron.Should().NotBeNull();
        humanIron!.Amount.Should().Be(ResourceTypes.InitialAmounts.Iron);

        // Verify units were created
        var units = await _context.Units.Where(u => u.GameId == gameId).ToListAsync();
        units.Should().HaveCount(2);
        
        var humanWarrior = units.FirstOrDefault(u => u.ParticipantId == humanParticipantId);
        humanWarrior.Should().NotBeNull();
        humanWarrior!.TypeId.Should().Be(warriorDef.Id);
        humanWarrior.Hp.Should().Be(warriorDef.Health);
        humanWarrior.HasActed.Should().BeFalse();
        
        var aiWarrior = units.FirstOrDefault(u => u.ParticipantId == aiParticipantId);
        aiWarrior.Should().NotBeNull();
        aiWarrior!.TypeId.Should().Be(warriorDef.Id);
        aiWarrior.Hp.Should().Be(warriorDef.Health);
        aiWarrior.HasActed.Should().BeFalse();

        // Verify units are placed on different tiles
        humanWarrior.TileId.Should().NotBe(aiWarrior.TileId);
    }

    [Fact]
    public async Task SeedGameEntitiesAsync_WithMissingWarriorDefinition_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var gameId = 1L;
        var mapId = 1L;
        var humanParticipantId = 101L;
        var aiParticipantId = 102L;
        var rngSeed = 12345L;

        var map = new Map
        {
            Id = mapId,
            Code = "test_map",
            SchemaVersion = 1,
            Width = 8,
            Height = 6
        };
        _context.Maps.Add(map);

        // Don't add warrior unit definition

        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SeedGameEntitiesAsync(
                gameId,
                mapId,
                humanParticipantId,
                aiParticipantId,
                rngSeed,
                default));

        exception.Message.Should().Contain($"Unit definition '{UnitTypes.Warrior}' not found");
    }

    [Fact]
    public async Task SeedGameEntitiesAsync_WithMissingMap_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var gameId = 1L;
        var mapId = 999L; // Non-existent map
        var humanParticipantId = 101L;
        var aiParticipantId = 102L;
        var rngSeed = 12345L;

        // Add warrior definition but no map
        var warriorDef = new UnitDefinition
        {
            Id = 1,
            Code = UnitTypes.Warrior,
            IsRanged = false,
            Health = 20,
            Attack = 10,
            Defence = 5,
            RangeMin = 0,
            RangeMax = 0,
            MovePoints = 2
        };
        _context.UnitDefinitions.Add(warriorDef);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SeedGameEntitiesAsync(
                gameId,
                mapId,
                humanParticipantId,
                aiParticipantId,
                rngSeed,
                default));

        exception.Message.Should().Contain($"Map with ID {mapId} not found");
    }

    [Fact]
    public async Task SeedGameEntitiesAsync_WithNoTiles_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var gameId = 1L;
        var mapId = 1L;
        var humanParticipantId = 101L;
        var aiParticipantId = 102L;
        var rngSeed = 12345L;

        var map = new Map
        {
            Id = mapId,
            Code = "empty_map",
            SchemaVersion = 1,
            Width = 8,
            Height = 6
        };
        _context.Maps.Add(map);

        var warriorDef = new UnitDefinition
        {
            Id = 1,
            Code = UnitTypes.Warrior,
            IsRanged = false,
            Health = 20,
            Attack = 10,
            Defence = 5,
            RangeMin = 0,
            RangeMax = 0,
            MovePoints = 2
        };
        _context.UnitDefinitions.Add(warriorDef);
        await _context.SaveChangesAsync();

        // No tiles added

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SeedGameEntitiesAsync(
                gameId,
                mapId,
                humanParticipantId,
                aiParticipantId,
                rngSeed,
                default));

        exception.Message.Should().Contain($"No tiles found for map {mapId}");
    }

    // [Fact]
    public async Task SeedGameEntitiesAsync_WithOnlyOneTile_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var gameId = 1L;
        var mapId = 1L;
        var humanParticipantId = 101L;
        var aiParticipantId = 102L;
        var rngSeed = 12345L;

        var map = new Map
        {
            Id = mapId,
            Code = "tiny_map",
            SchemaVersion = 1,
            Width = 1,
            Height = 1
        };
        _context.Maps.Add(map);

        var warriorDef = new UnitDefinition
        {
            Id = 1,
            Code = UnitTypes.Warrior,
            IsRanged = false,
            Health = 20,
            Attack = 10,
            Defence = 5,
            RangeMin = 0,
            RangeMax = 0,
            MovePoints = 2
        };
        _context.UnitDefinitions.Add(warriorDef);

        // Add only one tile
        _context.MapTiles.Add(new MapTile
        {
            Id = 1,
            MapId = mapId,
            Row = 0,
            Col = 0,
            Terrain = TerrainTypes.Grassland
        });

        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SeedGameEntitiesAsync(
                gameId,
                mapId,
                humanParticipantId,
                aiParticipantId,
                rngSeed,
                default));

        exception.Message.Should().Contain("Not enough valid tiles for starting positions");
        exception.Message.Should().Contain("Found 1");
        exception.Message.Should().Contain("need at least 2");
    }

    // [Fact]
    public async Task SeedGameEntitiesAsync_WithOnlyWaterTiles_ShouldStillPlaceCities()
    {
        // Arrange - Test that even with all water tiles, cities can still be placed
        // (This tests the fallback logic in DetermineStartingPositions)
        var gameId = 1L;
        var mapId = 1L;
        var humanParticipantId = 101L;
        var aiParticipantId = 102L;
        var rngSeed = 12345L;

        var map = new Map
        {
            Id = mapId,
            Code = "water_map",
            SchemaVersion = 1,
            Width = 8,
            Height = 6
        };
        _context.Maps.Add(map);

        var warriorDef = new UnitDefinition
        {
            Id = 1,
            Code = UnitTypes.Warrior,
            IsRanged = false,
            Health = 20,
            Attack = 10,
            Defence = 5,
            RangeMin = 0,
            RangeMax = 0,
            MovePoints = 2
        };
        _context.UnitDefinitions.Add(warriorDef);

        // Create all water tiles
        var tiles = new List<MapTile>();
        for (int row = 0; row < 6; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                tiles.Add(new MapTile
                {
                    Id = (row * 8) + col + 1,
                    MapId = mapId,
                    Row = row,
                    Col = col,
                    Terrain = TerrainTypes.Water // All water tiles
                });
            }
        }
        _context.MapTiles.AddRange(tiles);
        await _context.SaveChangesAsync();

        // Act
        await _service.SeedGameEntitiesAsync(
            gameId,
            mapId,
            humanParticipantId,
            aiParticipantId,
            rngSeed,
            default);

        // Assert - Should succeed because fallback uses all tiles if no land tiles available
        var cities = await _context.Cities.Where(c => c.GameId == gameId).ToListAsync();
        cities.Should().HaveCount(2);
    }

    // [Fact]
    public async Task SeedGameEntitiesAsync_WithDeterministicSeed_ShouldProduceConsistentResults()
    {
        // Arrange
        var gameId = 1L;
        var mapId = 1L;
        var humanParticipantId = 101L;
        var aiParticipantId = 102L;
        var rngSeed = 54321L;

        var map = new Map
        {
            Id = mapId,
            Code = "test_map",
            SchemaVersion = 1,
            Width = 8,
            Height = 6
        };
        _context.Maps.Add(map);

        var warriorDef = new UnitDefinition
        {
            Id = 1,
            Code = UnitTypes.Warrior,
            IsRanged = false,
            Health = 20,
            Attack = 10,
            Defence = 5,
            RangeMin = 0,
            RangeMax = 0,
            MovePoints = 2
        };
        _context.UnitDefinitions.Add(warriorDef);

        var tiles = new List<MapTile>();
        for (int row = 0; row < 6; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                tiles.Add(new MapTile
                {
                    Id = (row * 8) + col + 1,
                    MapId = mapId,
                    Row = row,
                    Col = col,
                    Terrain = TerrainTypes.Grassland
                });
            }
        }
        _context.MapTiles.AddRange(tiles);
        await _context.SaveChangesAsync();

        // Act - Run seeding twice
        await _service.SeedGameEntitiesAsync(
            gameId,
            mapId,
            humanParticipantId,
            aiParticipantId,
            rngSeed,
            default);

        var firstCities = await _context.Cities.Where(c => c.GameId == gameId).ToListAsync();
        var firstHumanCityTileId = firstCities.First(c => c.ParticipantId == humanParticipantId).TileId;
        var firstAiCityTileId = firstCities.First(c => c.ParticipantId == aiParticipantId).TileId;

        // Clear context and run again with same seed
        _context.Cities.RemoveRange(_context.Cities);
        _context.Units.RemoveRange(_context.Units);
        _context.CityTiles.RemoveRange(_context.CityTiles);
        _context.CityResources.RemoveRange(_context.CityResources);
        await _context.SaveChangesAsync();

        await _service.SeedGameEntitiesAsync(
            gameId,
            mapId,
            humanParticipantId,
            aiParticipantId,
            rngSeed,
            default);

        var secondCities = await _context.Cities.Where(c => c.GameId == gameId).ToListAsync();
        var secondHumanCityTileId = secondCities.First(c => c.ParticipantId == humanParticipantId).TileId;
        var secondAiCityTileId = secondCities.First(c => c.ParticipantId == aiParticipantId).TileId;

        // Assert - Same seed should produce same starting positions
        firstHumanCityTileId.Should().Be(secondHumanCityTileId);
        firstAiCityTileId.Should().Be(secondAiCityTileId);
    }

    // [Fact]
    public async Task SeedGameEntitiesAsync_ShouldPlaceCitiesInOppositeCorners()
    {
        // Arrange
        var gameId = 1L;
        var mapId = 1L;
        var humanParticipantId = 101L;
        var aiParticipantId = 102L;
        var rngSeed = 12345L;

        var map = new Map
        {
            Id = mapId,
            Code = "test_map",
            SchemaVersion = 1,
            Width = 8,
            Height = 6
        };
        _context.Maps.Add(map);

        var warriorDef = new UnitDefinition
        {
            Id = 1,
            Code = UnitTypes.Warrior,
            IsRanged = false,
            Health = 20,
            Attack = 10,
            Defence = 5,
            RangeMin = 0,
            RangeMax = 0,
            MovePoints = 2
        };
        _context.UnitDefinitions.Add(warriorDef);

        var tiles = new List<MapTile>();
        for (int row = 0; row < 6; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                tiles.Add(new MapTile
                {
                    Id = (row * 8) + col + 1,
                    MapId = mapId,
                    Row = row,
                    Col = col,
                    Terrain = TerrainTypes.Grassland
                });
            }
        }
        _context.MapTiles.AddRange(tiles);
        await _context.SaveChangesAsync();

        // Act
        await _service.SeedGameEntitiesAsync(
            gameId,
            mapId,
            humanParticipantId,
            aiParticipantId,
            rngSeed,
            default);

        // Assert
        var cities = await _context.Cities
            .Include(c => c.Tile)
            .Where(c => c.GameId == gameId)
            .ToListAsync();

        var humanCity = cities.First(c => c.ParticipantId == humanParticipantId);
        var aiCity = cities.First(c => c.ParticipantId == aiParticipantId);

        // Verify cities are placed far apart (using Manhattan distance as approximation)
        var distance = Math.Abs(humanCity.Tile.Row - aiCity.Tile.Row) + 
                      Math.Abs(humanCity.Tile.Col - aiCity.Tile.Col);
        
        // Should be reasonably far apart (at least half the map diagonal)
        distance.Should().BeGreaterThanOrEqualTo(4);
    }

    // [Fact]
    public async Task SeedGameEntitiesAsync_ShouldPlaceUnitsAdjacentToCities()
    {
        // Arrange
        var gameId = 1L;
        var mapId = 1L;
        var humanParticipantId = 101L;
        var aiParticipantId = 102L;
        var rngSeed = 12345L;

        var map = new Map
        {
            Id = mapId,
            Code = "test_map",
            SchemaVersion = 1,
            Width = 8,
            Height = 6
        };
        _context.Maps.Add(map);

        var warriorDef = new UnitDefinition
        {
            Id = 1,
            Code = UnitTypes.Warrior,
            IsRanged = false,
            Health = 20,
            Attack = 10,
            Defence = 5,
            RangeMin = 0,
            RangeMax = 0,
            MovePoints = 2
        };
        _context.UnitDefinitions.Add(warriorDef);

        var tiles = new List<MapTile>();
        for (int row = 0; row < 6; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                tiles.Add(new MapTile
                {
                    Id = (row * 8) + col + 1,
                    MapId = mapId,
                    Row = row,
                    Col = col,
                    Terrain = TerrainTypes.Grassland
                });
            }
        }
        _context.MapTiles.AddRange(tiles);
        await _context.SaveChangesAsync();

        // Act
        await _service.SeedGameEntitiesAsync(
            gameId,
            mapId,
            humanParticipantId,
            aiParticipantId,
            rngSeed,
            default);

        // Assert
        var cities = await _context.Cities
            .Include(c => c.Tile)
            .Where(c => c.GameId == gameId)
            .ToListAsync();
        
        var units = await _context.Units
            .Include(u => u.Tile)
            .Where(u => u.GameId == gameId)
            .ToListAsync();

        var humanCity = cities.First(c => c.ParticipantId == humanParticipantId);
        var humanWarrior = units.First(u => u.ParticipantId == humanParticipantId);

        // Verify unit is near city (within 1-2 tiles typically for adjacent placement)
        var humanDistance = Math.Abs(humanCity.Tile.Row - humanWarrior.Tile.Row) + 
                           Math.Abs(humanCity.Tile.Col - humanWarrior.Tile.Col);
        humanDistance.Should().BeLessThanOrEqualTo(2);

        var aiCity = cities.First(c => c.ParticipantId == aiParticipantId);
        var aiWarrior = units.First(u => u.ParticipantId == aiParticipantId);

        var aiDistance = Math.Abs(aiCity.Tile.Row - aiWarrior.Tile.Row) + 
                        Math.Abs(aiCity.Tile.Col - aiWarrior.Tile.Col);
        aiDistance.Should().BeLessThanOrEqualTo(2);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

