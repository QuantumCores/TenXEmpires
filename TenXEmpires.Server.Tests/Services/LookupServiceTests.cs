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

    private static TenXDbContext CreateInMemoryContext(string dbName = "TestDb")
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(databaseName: $"{dbName}_{Guid.NewGuid()}")
            .Options;

        return new TenXDbContext(options);
    }
}

