using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Services;

namespace TenXEmpires.Server.Tests.Services;

public class LookupServiceTests
{
    private readonly Mock<ILogger<LookupService>> _loggerMock;
    private readonly IMemoryCache _memoryCache;

    public LookupServiceTests()
    {
        _loggerMock = new Mock<ILogger<LookupService>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task GetUnitDefinitionsAsync_ShouldReturnAllUnitDefinitions()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        // Seed test data
        context.UnitDefinitions.AddRange(
            new UnitDefinition
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
            },
            new UnitDefinition
            {
                Id = 2,
                Code = "archer",
                IsRanged = true,
                Attack = 15,
                Defence = 5,
                RangeMin = 2,
                RangeMax = 3,
                MovePoints = 2,
                Health = 80
            }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetUnitDefinitionsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().ContainSingle(u => u.Code == "warrior");
        result.Should().ContainSingle(u => u.Code == "archer");
    }

    [Fact]
    public async Task GetUnitDefinitionsAsync_ShouldReturnUnitsOrderedByCode()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        // Seed test data in non-alphabetical order
        context.UnitDefinitions.AddRange(
            new UnitDefinition { Id = 1, Code = "warrior", IsRanged = false, Attack = 20, Defence = 10, RangeMin = 0, RangeMax = 0, MovePoints = 2, Health = 100 },
            new UnitDefinition { Id = 2, Code = "archer", IsRanged = true, Attack = 15, Defence = 5, RangeMin = 2, RangeMax = 3, MovePoints = 2, Health = 80 },
            new UnitDefinition { Id = 3, Code = "slinger", IsRanged = true, Attack = 10, Defence = 3, RangeMin = 1, RangeMax = 2, MovePoints = 2, Health = 60 }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetUnitDefinitionsAsync();

        // Assert
        result.Select(u => u.Code).Should().BeInAscendingOrder();
        result[0].Code.Should().Be("archer");
        result[1].Code.Should().Be("slinger");
        result[2].Code.Should().Be("warrior");
    }

    [Fact]
    public async Task GetUnitDefinitionsAsync_ShouldProjectToDto()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        context.UnitDefinitions.Add(new UnitDefinition
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
        });
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetUnitDefinitionsAsync();

        // Assert
        var unitDto = result.First();
        unitDto.Should().BeOfType<UnitDefinitionDto>();
        unitDto.Id.Should().Be(1);
        unitDto.Code.Should().Be("warrior");
        unitDto.IsRanged.Should().BeFalse();
        unitDto.Attack.Should().Be(20);
        unitDto.Defence.Should().Be(10);
        unitDto.RangeMin.Should().Be(0);
        unitDto.RangeMax.Should().Be(0);
        unitDto.MovePoints.Should().Be(2);
        unitDto.Health.Should().Be(100);
    }

    [Fact]
    public async Task GetUnitDefinitionsAsync_ShouldCacheResults()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        context.UnitDefinitions.Add(new UnitDefinition
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
        });
        await context.SaveChangesAsync();

        // Act - First call
        var result1 = await service.GetUnitDefinitionsAsync();

        // Add another unit to database (should not be returned due to caching)
        context.UnitDefinitions.Add(new UnitDefinition
        {
            Id = 2,
            Code = "archer",
            IsRanged = true,
            Attack = 15,
            Defence = 5,
            RangeMin = 2,
            RangeMax = 3,
            MovePoints = 2,
            Health = 80
        });
        await context.SaveChangesAsync();

        // Act - Second call (should use cache)
        var result2 = await service.GetUnitDefinitionsAsync();

        // Assert
        result1.Should().HaveCount(1);
        result2.Should().HaveCount(1); // Still 1 because cached
        result1.Should().BeSameAs(result2); // Same instance from cache
    }

    [Fact]
    public async Task GetUnitDefinitionsETagAsync_ShouldReturnValidETag()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        context.UnitDefinitions.Add(new UnitDefinition
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
        });
        await context.SaveChangesAsync();

        // Act
        var etag = await service.GetUnitDefinitionsETagAsync();

        // Assert
        etag.Should().NotBeNullOrEmpty();
        etag.Should().StartWith("\"");
        etag.Should().EndWith("\"");
    }

    [Fact]
    public async Task GetUnitDefinitionsETagAsync_ShouldReturnDifferentETagForDifferentData()
    {
        // Arrange
        var context1 = CreateInMemoryContext("db1");
        var service1 = new LookupService(context1, new MemoryCache(new MemoryCacheOptions()), _loggerMock.Object);

        var context2 = CreateInMemoryContext("db2");
        var service2 = new LookupService(context2, new MemoryCache(new MemoryCacheOptions()), _loggerMock.Object);

        context1.UnitDefinitions.Add(new UnitDefinition { Id = 1, Code = "warrior", IsRanged = false, Attack = 20, Defence = 10, RangeMin = 0, RangeMax = 0, MovePoints = 2, Health = 100 });
        await context1.SaveChangesAsync();

        context2.UnitDefinitions.Add(new UnitDefinition { Id = 1, Code = "archer", IsRanged = true, Attack = 15, Defence = 5, RangeMin = 2, RangeMax = 3, MovePoints = 2, Health = 80 });
        await context2.SaveChangesAsync();

        // Act
        var etag1 = await service1.GetUnitDefinitionsETagAsync();
        var etag2 = await service2.GetUnitDefinitionsETagAsync();

        // Assert
        etag1.Should().NotBe(etag2);
    }

    [Fact]
    public async Task GetUnitDefinitionsETagAsync_ShouldReturnSameETagForSameData()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        context.UnitDefinitions.Add(new UnitDefinition { Id = 1, Code = "warrior", IsRanged = false, Attack = 20, Defence = 10, RangeMin = 0, RangeMax = 0, MovePoints = 2, Health = 100 });
        await context.SaveChangesAsync();

        // Act
        var etag1 = await service.GetUnitDefinitionsETagAsync();
        var etag2 = await service.GetUnitDefinitionsETagAsync();

        // Assert
        etag1.Should().Be(etag2);
    }

    [Fact]
    public async Task GetUnitDefinitionsAsync_ShouldReturnEmptyListWhenNoData()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        // Act
        var result = await service.GetUnitDefinitionsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #region ComputeMapETag Tests

    [Fact]
    public void ComputeMapETag_ShouldReturnValidETag()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);
        var map = new MapDto(1, "map-01", 1, 20, 30);

        // Act
        var etag = service.ComputeMapETag(map);

        // Assert
        etag.Should().NotBeNullOrEmpty();
        etag.Should().StartWith("\"");
        etag.Should().EndWith("\"");
    }

    [Fact]
    public void ComputeMapETag_ShouldReturnSameETagForSameMap()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);
        var map1 = new MapDto(1, "map-01", 1, 20, 30);
        var map2 = new MapDto(1, "map-01", 1, 20, 30);

        // Act
        var etag1 = service.ComputeMapETag(map1);
        var etag2 = service.ComputeMapETag(map2);

        // Assert
        etag1.Should().Be(etag2);
    }

    [Fact]
    public void ComputeMapETag_ShouldReturnDifferentETagForDifferentMaps()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);
        var map1 = new MapDto(1, "map-01", 1, 20, 30);
        var map2 = new MapDto(2, "map-02", 1, 20, 30); // Different code
        var map3 = new MapDto(1, "map-01", 2, 20, 30); // Different schema version
        var map4 = new MapDto(1, "map-01", 1, 25, 30); // Different width

        // Act
        var etag1 = service.ComputeMapETag(map1);
        var etag2 = service.ComputeMapETag(map2);
        var etag3 = service.ComputeMapETag(map3);
        var etag4 = service.ComputeMapETag(map4);

        // Assert
        etag1.Should().NotBe(etag2);
        etag1.Should().NotBe(etag3);
        etag1.Should().NotBe(etag4);
    }

    #endregion

    #region GetMapTilesAsync Tests

    [Fact]
    public async Task GetMapTilesAsync_ShouldReturnTilesForValidMap()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        var map = new Map { Id = 1, Code = "test-map", SchemaVersion = 1, Width = 10, Height = 10 };
        context.Maps.Add(map);
        context.MapTiles.AddRange(
            new MapTile { Id = 1, MapId = 1, Row = 0, Col = 0, Terrain = "grassland", ResourceType = "wheat", ResourceAmount = 2, Map = map },
            new MapTile { Id = 2, MapId = 1, Row = 0, Col = 1, Terrain = "plains", ResourceType = null, ResourceAmount = 0, Map = map },
            new MapTile { Id = 3, MapId = 1, Row = 1, Col = 0, Terrain = "hills", ResourceType = "iron", ResourceAmount = 3, Map = map }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetMapTilesAsync("test-map");

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Total.Should().Be(3);
    }

    [Fact]
    public async Task GetMapTilesAsync_ShouldReturnNullForNonExistentMap()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        // Act
        var result = await service.GetMapTilesAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMapTilesAsync_ShouldOrderTilesByRowThenCol()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        var map = new Map { Id = 1, Code = "test-map", SchemaVersion = 1, Width = 3, Height = 3 };
        context.Maps.Add(map);
        // Add tiles in non-sequential order
        context.MapTiles.AddRange(
            new MapTile { Id = 5, MapId = 1, Row = 2, Col = 1, Terrain = "grassland", Map = map },
            new MapTile { Id = 1, MapId = 1, Row = 0, Col = 0, Terrain = "plains", Map = map },
            new MapTile { Id = 3, MapId = 1, Row = 1, Col = 0, Terrain = "hills", Map = map },
            new MapTile { Id = 2, MapId = 1, Row = 0, Col = 1, Terrain = "forest", Map = map },
            new MapTile { Id = 4, MapId = 1, Row = 1, Col = 1, Terrain = "mountain", Map = map }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetMapTilesAsync("test-map");

        // Assert
        result.Should().NotBeNull();
        var tiles = result!.Items.ToList();
        tiles[0].Row.Should().Be(0);
        tiles[0].Col.Should().Be(0);
        tiles[1].Row.Should().Be(0);
        tiles[1].Col.Should().Be(1);
        tiles[2].Row.Should().Be(1);
        tiles[2].Col.Should().Be(0);
        tiles[3].Row.Should().Be(1);
        tiles[3].Col.Should().Be(1);
        tiles[4].Row.Should().Be(2);
        tiles[4].Col.Should().Be(1);
    }

    [Fact]
    public async Task GetMapTilesAsync_ShouldApplyPagination()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        var map = new Map { Id = 1, Code = "test-map", SchemaVersion = 1, Width = 10, Height = 10 };
        context.Maps.Add(map);
        
        // Add 25 tiles
        for (int i = 0; i < 25; i++)
        {
            context.MapTiles.Add(new MapTile 
            { 
                Id = i + 1, 
                MapId = 1, 
                Row = i / 5, 
                Col = i % 5, 
                Terrain = "grassland", 
                Map = map 
            });
        }
        await context.SaveChangesAsync();

        // Act - First page with pageSize 10
        var page1 = await service.GetMapTilesAsync("test-map", page: 1, pageSize: 10);
        var page2 = await service.GetMapTilesAsync("test-map", page: 2, pageSize: 10);
        var page3 = await service.GetMapTilesAsync("test-map", page: 3, pageSize: 10);

        // Assert
        page1.Should().NotBeNull();
        page1!.Items.Should().HaveCount(10);
        page1.Page.Should().Be(1);
        page1.PageSize.Should().Be(10);
        page1.Total.Should().Be(25);

        page2.Should().NotBeNull();
        page2!.Items.Should().HaveCount(10);
        page2.Page.Should().Be(2);
        page2.Total.Should().BeNull(); // Only first page has total

        page3.Should().NotBeNull();
        page3!.Items.Should().HaveCount(5); // Last page has remaining items
        page3.Page.Should().Be(3);
    }

    [Fact]
    public async Task GetMapTilesAsync_ShouldUseDefaultPageSize()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        var map = new Map { Id = 1, Code = "test-map", SchemaVersion = 1, Width = 5, Height = 5 };
        context.Maps.Add(map);
        context.MapTiles.Add(new MapTile { Id = 1, MapId = 1, Row = 0, Col = 0, Terrain = "grassland", Map = map });
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetMapTilesAsync("test-map");

        // Assert
        result.Should().NotBeNull();
        result!.PageSize.Should().Be(20); // Default page size
        result.Page.Should().Be(1); // Default page
    }

    [Fact]
    public async Task GetMapTilesAsync_ShouldThrowForInvalidPage()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetMapTilesAsync("test-map", page: 0));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetMapTilesAsync("test-map", page: -1));
    }

    [Fact]
    public async Task GetMapTilesAsync_ShouldThrowForInvalidPageSize()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetMapTilesAsync("test-map", pageSize: 0));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetMapTilesAsync("test-map", pageSize: -1));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetMapTilesAsync("test-map", pageSize: 101));
    }

    [Fact]
    public async Task GetMapTilesAsync_ShouldThrowForNullOrWhiteSpaceCode()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetMapTilesAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetMapTilesAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetMapTilesAsync("   "));
    }

    [Fact]
    public async Task GetMapTilesAsync_ShouldCacheResults()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        var map = new Map { Id = 1, Code = "test-map", SchemaVersion = 1, Width = 5, Height = 5 };
        context.Maps.Add(map);
        context.MapTiles.Add(new MapTile { Id = 1, MapId = 1, Row = 0, Col = 0, Terrain = "grassland", Map = map });
        await context.SaveChangesAsync();

        // Act - First call
        var result1 = await service.GetMapTilesAsync("test-map");

        // Add another tile (should not appear due to caching)
        context.MapTiles.Add(new MapTile { Id = 2, MapId = 1, Row = 0, Col = 1, Terrain = "plains", Map = map });
        await context.SaveChangesAsync();

        // Act - Second call (should use cache)
        var result2 = await service.GetMapTilesAsync("test-map");

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.Items.Should().HaveCount(1);
        result2!.Items.Should().HaveCount(1); // Still 1 because cached
    }

    [Fact]
    public async Task GetMapTilesAsync_ShouldProjectToDto()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        var map = new Map { Id = 1, Code = "test-map", SchemaVersion = 1, Width = 5, Height = 5 };
        context.Maps.Add(map);
        context.MapTiles.Add(new MapTile 
        { 
            Id = 42, 
            MapId = 1, 
            Row = 3, 
            Col = 7, 
            Terrain = "mountain", 
            ResourceType = "gold", 
            ResourceAmount = 5,
            Map = map 
        });
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetMapTilesAsync("test-map");

        // Assert
        result.Should().NotBeNull();
        var tile = result!.Items.First();
        tile.Should().BeOfType<MapTileDto>();
        tile.Id.Should().Be(42);
        tile.Row.Should().Be(3);
        tile.Col.Should().Be(7);
        tile.Terrain.Should().Be("mountain");
        tile.ResourceType.Should().Be("gold");
        tile.ResourceAmount.Should().Be(5);
    }

    #endregion

    #region GetMapTilesETagAsync Tests

    [Fact]
    public async Task GetMapTilesETagAsync_ShouldReturnValidETag()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        var map = new Map { Id = 1, Code = "test-map", SchemaVersion = 1, Width = 5, Height = 5 };
        context.Maps.Add(map);
        context.MapTiles.Add(new MapTile { Id = 1, MapId = 1, Row = 0, Col = 0, Terrain = "grassland", Map = map });
        await context.SaveChangesAsync();

        // Act
        var etag = await service.GetMapTilesETagAsync("test-map");

        // Assert
        etag.Should().NotBeNullOrEmpty();
        etag.Should().StartWith("\"");
        etag.Should().EndWith("\"");
    }

    [Fact]
    public async Task GetMapTilesETagAsync_ShouldReturnNullForNonExistentMap()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        // Act
        var etag = await service.GetMapTilesETagAsync("non-existent");

        // Assert
        etag.Should().BeNull();
    }

    [Fact]
    public async Task GetMapTilesETagAsync_ShouldVaryByPagination()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, new MemoryCache(new MemoryCacheOptions()), _loggerMock.Object);

        var map = new Map { Id = 1, Code = "test-map", SchemaVersion = 1, Width = 10, Height = 10 };
        context.Maps.Add(map);
        for (int i = 0; i < 30; i++)
        {
            context.MapTiles.Add(new MapTile { Id = i + 1, MapId = 1, Row = i / 5, Col = i % 5, Terrain = "grassland", Map = map });
        }
        await context.SaveChangesAsync();

        // Act
        var etagPage1Size10 = await service.GetMapTilesETagAsync("test-map", page: 1, pageSize: 10);
        var etagPage2Size10 = await service.GetMapTilesETagAsync("test-map", page: 2, pageSize: 10);
        var etagPage1Size20 = await service.GetMapTilesETagAsync("test-map", page: 1, pageSize: 20);

        // Assert
        etagPage1Size10.Should().NotBe(etagPage2Size10); // Different pages
        etagPage1Size10.Should().NotBe(etagPage1Size20); // Different page sizes
    }

    [Fact]
    public async Task GetMapTilesETagAsync_ShouldReturnSameETagForSameParameters()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new LookupService(context, _memoryCache, _loggerMock.Object);

        var map = new Map { Id = 1, Code = "test-map", SchemaVersion = 1, Width = 5, Height = 5 };
        context.Maps.Add(map);
        context.MapTiles.Add(new MapTile { Id = 1, MapId = 1, Row = 0, Col = 0, Terrain = "grassland", Map = map });
        await context.SaveChangesAsync();

        // Act
        var etag1 = await service.GetMapTilesETagAsync("test-map", page: 1, pageSize: 20);
        var etag2 = await service.GetMapTilesETagAsync("test-map", page: 1, pageSize: 20);

        // Assert
        etag1.Should().Be(etag2);
    }

    #endregion

    private static TenXDbContext CreateInMemoryContext(string dbName = "TestDb")
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(databaseName: $"{dbName}_{Guid.NewGuid()}")
            .Options;

        return new TenXDbContext(options);
    }
}

