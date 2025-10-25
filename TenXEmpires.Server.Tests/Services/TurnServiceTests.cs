using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Services;

namespace TenXEmpires.Server.Tests.Services;

public class TurnServiceTests : IDisposable
{
    private readonly TenXDbContext _context;
    private readonly TurnService _service;
    private readonly Mock<ILogger<TurnService>> _loggerMock;
    private readonly Guid _testUserId;
    private readonly long _testGameId = 1L;

    public TurnServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TenXDbContext(options);
        _loggerMock = new Mock<ILogger<TurnService>>();

        _service = new TurnService(
            _context,
            _loggerMock.Object);

        _testUserId = Guid.NewGuid();

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var now = DateTimeOffset.UtcNow;

        // Create a test game
        var game = new Game
        {
            Id = _testGameId,
            UserId = _testUserId,
            MapId = 1,
            MapSchemaVersion = 1,
            TurnNo = 5,
            Status = "active",
            StartedAt = now.AddHours(-5),
            LastTurnAt = now,
            RngSeed = 12345,
            Settings = "{}"
        };
        _context.Games.Add(game);

        // Create participants
        var participant1 = new Participant
        {
            Id = 1,
            GameId = _testGameId,
            Kind = "human",
            UserId = _testUserId,
            DisplayName = "Player",
            IsEliminated = false
        };

        var participant2 = new Participant
        {
            Id = 2,
            GameId = _testGameId,
            Kind = "ai",
            UserId = null,
            DisplayName = "AI",
            IsEliminated = false
        };

        _context.Participants.AddRange(participant1, participant2);

        // Create 5 turns for the game
        _context.Turns.AddRange(
            new Turn
            {
                Id = 1,
                GameId = _testGameId,
                TurnNo = 1,
                ParticipantId = 1,
                CommittedAt = now.AddHours(-5),
                DurationMs = 10000,
                Summary = @"{""actions"": 2, ""unitsMovedCount"": 1}"
            },
            new Turn
            {
                Id = 2,
                GameId = _testGameId,
                TurnNo = 2,
                ParticipantId = 2,
                CommittedAt = now.AddHours(-4),
                DurationMs = 8000,
                Summary = @"{""actions"": 1, ""unitsMovedCount"": 0}"
            },
            new Turn
            {
                Id = 3,
                GameId = _testGameId,
                TurnNo = 3,
                ParticipantId = 1,
                CommittedAt = now.AddHours(-3),
                DurationMs = 15000,
                Summary = @"{""actions"": 4, ""unitsMovedCount"": 3}"
            },
            new Turn
            {
                Id = 4,
                GameId = _testGameId,
                TurnNo = 4,
                ParticipantId = 2,
                CommittedAt = now.AddHours(-2),
                DurationMs = 6000,
                Summary = null // Test null summary
            },
            new Turn
            {
                Id = 5,
                GameId = _testGameId,
                TurnNo = 5,
                ParticipantId = 1,
                CommittedAt = now.AddHours(-1),
                DurationMs = 12000,
                Summary = @"{""actions"": 3, ""unitsMovedCount"": 2}"
            }
        );

        // Create another game with no turns to test empty results
        var emptyGame = new Game
        {
            Id = 2,
            UserId = _testUserId,
            MapId = 1,
            MapSchemaVersion = 1,
            TurnNo = 1,
            Status = "active",
            StartedAt = now,
            RngSeed = 12346,
            Settings = "{}"
        };
        _context.Games.Add(emptyGame);

        _context.SaveChanges();
    }

    [Fact]
    public async Task ListTurnsAsync_WithDefaultParameters_ShouldReturnAllTurnsSortedByTurnNoDesc()
    {
        // Arrange
        var query = new ListTurnsQuery();

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        result.Items.Should().HaveCount(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Total.Should().Be(5);
        result.Items.Should().BeInDescendingOrder(t => t.TurnNo);
        result.Items[0].TurnNo.Should().Be(5); // Most recent turn first
        result.Items[4].TurnNo.Should().Be(1); // Oldest turn last
    }

    [Fact]
    public async Task ListTurnsAsync_SortByTurnNoAsc_ShouldReturnTurnsInCorrectOrder()
    {
        // Arrange
        var query = new ListTurnsQuery { Sort = "turnNo", Order = "asc" };

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        result.Items.Should().HaveCount(5);
        result.Items.Should().BeInAscendingOrder(t => t.TurnNo);
        result.Items[0].TurnNo.Should().Be(1);
        result.Items[4].TurnNo.Should().Be(5);
    }

    [Fact]
    public async Task ListTurnsAsync_SortByTurnNoDesc_ShouldReturnTurnsInCorrectOrder()
    {
        // Arrange
        var query = new ListTurnsQuery { Sort = "turnNo", Order = "desc" };

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        result.Items.Should().HaveCount(5);
        result.Items.Should().BeInDescendingOrder(t => t.TurnNo);
        result.Items[0].TurnNo.Should().Be(5);
        result.Items[4].TurnNo.Should().Be(1);
    }

    [Fact]
    public async Task ListTurnsAsync_SortByCommittedAtAsc_ShouldReturnTurnsInCorrectOrder()
    {
        // Arrange
        var query = new ListTurnsQuery { Sort = "committedAt", Order = "asc" };

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        result.Items.Should().HaveCount(5);
        result.Items.Should().BeInAscendingOrder(t => t.CommittedAt);
    }

    [Fact]
    public async Task ListTurnsAsync_SortByCommittedAtDesc_ShouldReturnTurnsInCorrectOrder()
    {
        // Arrange
        var query = new ListTurnsQuery { Sort = "committedAt", Order = "desc" };

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        result.Items.Should().HaveCount(5);
        result.Items.Should().BeInDescendingOrder(t => t.CommittedAt);
    }

    [Fact]
    public async Task ListTurnsAsync_WithInvalidSortField_ShouldThrowArgumentException()
    {
        // Arrange
        var query = new ListTurnsQuery { Sort = "invalidField" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ListTurnsAsync(_testGameId, query));

        exception.ParamName.Should().Be("Sort");
        exception.Message.Should().Contain("Invalid sort field");
    }

    [Fact]
    public async Task ListTurnsAsync_WithInvalidOrder_ShouldThrowArgumentException()
    {
        // Arrange
        var query = new ListTurnsQuery { Order = "sideways" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ListTurnsAsync(_testGameId, query));

        exception.ParamName.Should().Be("Order");
        exception.Message.Should().Contain("Invalid order");
    }

    [Fact]
    public async Task ListTurnsAsync_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var query = new ListTurnsQuery { Page = 2, PageSize = 2 };

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        result.Items.Should().HaveCount(2); // Items 3 and 4 (in desc order: turns 3 and 2)
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Total.Should().Be(5);
    }

    [Fact]
    public async Task ListTurnsAsync_WithPageSizeLargerThanTotal_ShouldReturnAllItems()
    {
        // Arrange
        var query = new ListTurnsQuery { Page = 1, PageSize = 100 };

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        result.Items.Should().HaveCount(5);
        result.Total.Should().Be(5);
    }

    [Fact]
    public async Task ListTurnsAsync_WithPageBeyondTotal_ShouldReturnEmptyItems()
    {
        // Arrange
        var query = new ListTurnsQuery { Page = 10, PageSize = 20 };

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(5);
    }

    [Fact]
    public async Task ListTurnsAsync_ForGameWithNoTurns_ShouldReturnEmptyResult()
    {
        // Arrange
        var emptyGameId = 2L;
        var query = new ListTurnsQuery();

        // Act
        var result = await _service.ListTurnsAsync(emptyGameId, query);

        // Assert
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task ListTurnsAsync_ForNonExistentGame_ShouldReturnEmptyResult()
    {
        // Arrange
        var nonExistentGameId = 999L;
        var query = new ListTurnsQuery();

        // Act
        var result = await _service.ListTurnsAsync(nonExistentGameId, query);

        // Assert
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task ListTurnsAsync_ShouldIncludeAllTurnFields()
    {
        // Arrange
        var query = new ListTurnsQuery { Sort = "turnNo", Order = "asc" };

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        var firstTurn = result.Items.First();
        firstTurn.Id.Should().Be(1);
        firstTurn.TurnNo.Should().Be(1);
        firstTurn.ParticipantId.Should().Be(1);
        firstTurn.CommittedAt.Should().NotBe(default(DateTimeOffset));
        firstTurn.DurationMs.Should().Be(10000);
        firstTurn.Summary.Should().NotBeNull();
    }

    [Fact]
    public async Task ListTurnsAsync_ShouldParseJsonSummary()
    {
        // Arrange
        var query = new ListTurnsQuery { Sort = "turnNo", Order = "asc" };

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        var firstTurn = result.Items.First();
        firstTurn.Summary.Should().NotBeNull();
        firstTurn.Summary!.RootElement.GetProperty("actions").GetInt32().Should().Be(2);
        firstTurn.Summary.RootElement.GetProperty("unitsMovedCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ListTurnsAsync_ShouldHandleNullSummary()
    {
        // Arrange
        var query = new ListTurnsQuery { Sort = "turnNo", Order = "asc" };

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        var turnWithNullSummary = result.Items.FirstOrDefault(t => t.TurnNo == 4);
        turnWithNullSummary.Should().NotBeNull();
        turnWithNullSummary!.Summary.Should().BeNull();
    }

    [Fact]
    public async Task ListTurnsAsync_CaseInsensitiveSortAndOrder_ShouldWork()
    {
        // Arrange
        var query = new ListTurnsQuery { Sort = "TURNNO", Order = "ASC" };

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        result.Items.Should().BeInAscendingOrder(t => t.TurnNo);
    }

    [Fact]
    public async Task ListTurnsAsync_WithSmallPageSize_ShouldPaginateCorrectly()
    {
        // Arrange & Act
        var page1 = await _service.ListTurnsAsync(_testGameId, new ListTurnsQuery { Page = 1, PageSize = 2 });
        var page2 = await _service.ListTurnsAsync(_testGameId, new ListTurnsQuery { Page = 2, PageSize = 2 });
        var page3 = await _service.ListTurnsAsync(_testGameId, new ListTurnsQuery { Page = 3, PageSize = 2 });

        // Assert
        page1.Items.Should().HaveCount(2);
        page1.Items[0].TurnNo.Should().Be(5);
        page1.Items[1].TurnNo.Should().Be(4);

        page2.Items.Should().HaveCount(2);
        page2.Items[0].TurnNo.Should().Be(3);
        page2.Items[1].TurnNo.Should().Be(2);

        page3.Items.Should().HaveCount(1);
        page3.Items[0].TurnNo.Should().Be(1);

        page1.Total.Should().Be(5);
        page2.Total.Should().Be(5);
        page3.Total.Should().Be(5);
    }

    [Fact]
    public async Task ListTurnsAsync_WithMultipleParameters_ShouldApplyAllCorrectly()
    {
        // Arrange
        var query = new ListTurnsQuery
        {
            Sort = "committedAt",
            Order = "asc",
            Page = 1,
            PageSize = 3
        };

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items.Should().BeInAscendingOrder(t => t.CommittedAt);
        result.Total.Should().Be(5);
    }

    [Fact]
    public async Task ListTurnsAsync_ShouldFilterByGameId()
    {
        // Arrange
        // Add turns for another game
        var otherGameId = 3L;
        var otherGame = new Game
        {
            Id = otherGameId,
            UserId = _testUserId,
            MapId = 1,
            MapSchemaVersion = 1,
            TurnNo = 1,
            Status = "active",
            StartedAt = DateTimeOffset.UtcNow,
            RngSeed = 99999,
            Settings = "{}"
        };
        _context.Games.Add(otherGame);

        _context.Turns.Add(new Turn
        {
            Id = 100,
            GameId = otherGameId,
            TurnNo = 1,
            ParticipantId = 1,
            CommittedAt = DateTimeOffset.UtcNow,
            DurationMs = 5000
        });
        await _context.SaveChangesAsync();

        var query = new ListTurnsQuery();

        // Act
        var result = await _service.ListTurnsAsync(_testGameId, query);

        // Assert
        result.Items.Should().HaveCount(5);
        result.Items.Should().NotContain(t => t.Id == 100);
        result.Items.Should().OnlyContain(t => t.TurnNo >= 1 && t.TurnNo <= 5);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

