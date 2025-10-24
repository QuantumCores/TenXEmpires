using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Services;
using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Tests.Services;

public class GameServiceTests : IDisposable
{
    private readonly TenXDbContext _context;
    private readonly GameService _service;
    private readonly Mock<ILogger<GameService>> _loggerMock;
    private readonly Guid _testUserId;

    public GameServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TenXDbContext(options);
        _loggerMock = new Mock<ILogger<GameService>>();
        _service = new GameService(_context, _loggerMock.Object);
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

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

