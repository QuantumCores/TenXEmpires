using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Controllers;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;

namespace TenXEmpires.Server.Tests.Controllers;

/// <summary>
/// Tests for GamesController focusing on HTTP concerns:
/// - Authentication and authorization
/// - Model binding and validation
/// - HTTP response codes and formats
/// - Error handling at the controller boundary
/// 
/// Business logic tests are in GameServiceTests.
/// </summary>
public class GamesControllerTests
{
    private readonly Mock<IGameService> _gameServiceMock;
    private readonly Mock<IGameStateService> _gameStateServiceMock;
    private readonly Mock<ILogger<GamesController>> _loggerMock;
    private readonly GamesController _controller;
    private readonly Guid _testUserId;

    public GamesControllerTests()
    {
        _gameServiceMock = new Mock<IGameService>();
        _gameStateServiceMock = new Mock<IGameStateService>();
        _loggerMock = new Mock<ILogger<GamesController>>();
        _testUserId = Guid.NewGuid();
        
        _controller = new GamesController(
            _gameServiceMock.Object,
            _gameStateServiceMock.Object,
            _loggerMock.Object);

        // Setup authenticated user context
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString()),
            new Claim(ClaimTypes.Name, "testuser@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };
    }

    [Fact]
    public async Task GetGameDetail_WithValidOwner_ShouldReturn200AndSetEtag()
    {
        // Arrange
        var id = 42L;
        var lastTurnAt = DateTimeOffset.UtcNow;
        var detail = new GameDetailDto(
            Id: id,
            UserId: _testUserId,
            MapId: 1,
            MapSchemaVersion: 1,
            TurnNo: 5,
            ActiveParticipantId: 101,
            TurnInProgress: false,
            Status: "active",
            StartedAt: lastTurnAt.AddHours(-2),
            FinishedAt: null,
            LastTurnAt: lastTurnAt,
            Settings: null);

        _gameServiceMock
            .Setup(s => s.GetGameDetailAsync(_testUserId, id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        // Act
        var result = await _controller.GetGameDetail(id, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().BeOfType<GameDetailDto>();

        _controller.Response.Headers.ContainsKey("ETag").Should().BeTrue();

        var expectedEtag = $"\"g:{id}:t:{detail.TurnNo}:ts:{detail.LastTurnAt?.ToUnixTimeSeconds() ?? 0}\"";
        _controller.Response.Headers["ETag"].ToString().Should().Be(expectedEtag);
    }

    [Fact]
    public async Task GetGameDetail_WithMatchingIfNoneMatch_ShouldReturn304()
    {
        // Arrange
        var id = 43L;
        var lastTurnAt = DateTimeOffset.UtcNow;
        var detail = new GameDetailDto(
            Id: id,
            UserId: _testUserId,
            MapId: 2,
            MapSchemaVersion: 1,
            TurnNo: 3,
            ActiveParticipantId: 201,
            TurnInProgress: false,
            Status: "active",
            StartedAt: lastTurnAt.AddHours(-3),
            FinishedAt: null,
            LastTurnAt: lastTurnAt,
            Settings: null);

        _gameServiceMock
            .Setup(s => s.GetGameDetailAsync(_testUserId, id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var expectedEtag = $"\"g:{id}:t:{detail.TurnNo}:ts:{detail.LastTurnAt?.ToUnixTimeSeconds() ?? 0}\"";
        _controller.Request.Headers["If-None-Match"] = expectedEtag;

        // Act
        var result = await _controller.GetGameDetail(id, CancellationToken.None);

        // Assert
        var status = result.Result.Should().BeOfType<StatusCodeResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status304NotModified);
        _controller.Response.Headers["ETag"].ToString().Should().Be(expectedEtag);
    }

    [Fact]
    public async Task GetGameDetail_NotFoundOrNoAccess_ShouldReturn404()
    {
        // Arrange
        var id = 44L;
        _gameServiceMock
            .Setup(s => s.GetGameDetailAsync(_testUserId, id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameDetailDto?)null);

        // Act
        var result = await _controller.GetGameDetail(id, CancellationToken.None);

        // Assert
        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ListGames_WithValidRequest_ShouldReturn200WithPagedResult()
    {
        // Arrange
        var query = new ListGamesQuery();
        var expectedResult = new PagedResult<GameListItemDto>
        {
            Items = new List<GameListItemDto>
            {
                new GameListItemDto(1, "active", 5, 1, 1, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow)
            },
            Page = 1,
            PageSize = 20,
            Total = 1
        };

        _gameServiceMock
            .Setup(s => s.ListGamesAsync(_testUserId, It.IsAny<ListGamesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.ListGames(query, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeOfType<PagedResult<GameListItemDto>>();
    }

    [Fact]
    public async Task ListGames_WithServiceException_ShouldCatchAndReturn400()
    {
        // Arrange
        var query = new ListGamesQuery { Status = "invalid" };
        _gameServiceMock
            .Setup(s => s.ListGamesAsync(_testUserId, query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid status", "Status"));

        // Act
        var result = await _controller.ListGames(query, CancellationToken.None);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListGames_ShouldPassCorrectUserIdToService()
    {
        // Arrange
        var query = new ListGamesQuery();
        var expectedResult = new PagedResult<GameListItemDto>
        {
            Items = new List<GameListItemDto>(),
            Page = 1,
            PageSize = 20,
            Total = 0
        };

        _gameServiceMock
            .Setup(s => s.ListGamesAsync(_testUserId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        await _controller.ListGames(query, CancellationToken.None);

        // Assert
        _gameServiceMock.Verify(
            s => s.ListGamesAsync(_testUserId, query, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ListGames_WithInvalidPage_ShouldReturnBadRequest(int invalidPage)
    {
        // Arrange
        var query = new ListGamesQuery { Page = invalidPage };
        _controller.ModelState.AddModelError("Page", "Page must be >= 1");

        // Act
        var result = await _controller.ListGames(query, CancellationToken.None);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ListGames_WithInvalidPageSize_ShouldReturnBadRequest(int invalidPageSize)
    {
        // Arrange
        var query = new ListGamesQuery { PageSize = invalidPageSize };
        _controller.ModelState.AddModelError("PageSize", "PageSize must be between 1 and 100");

        // Act
        var result = await _controller.ListGames(query, CancellationToken.None);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }


    [Fact]
    public async Task ListGames_WithNoAuthenticatedUser_ShouldReturnUnauthorized()
    {
        // Arrange
        var query = new ListGamesQuery();
        var controllerWithoutAuth = new GamesController(
            _gameServiceMock.Object,
            _gameStateServiceMock.Object,
            _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // Act
        var result = await controllerWithoutAuth.ListGames(query, CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task ListGames_WithInvalidUserIdClaim_ShouldReturnUnauthorized()
    {
        // Arrange
        var query = new ListGamesQuery();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "invalid-guid")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var controllerWithInvalidClaim = new GamesController(
            _gameServiceMock.Object,
            _gameStateServiceMock.Object,
            _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = claimsPrincipal
                }
            }
        };

        // Act
        var result = await controllerWithInvalidClaim.ListGames(query, CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task ListGames_WhenServiceThrowsUnhandledException_ShouldReturn500()
    {
        // Arrange
        var query = new ListGamesQuery();
        _gameServiceMock
            .Setup(s => s.ListGamesAsync(_testUserId, query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected database error"));

        // Act
        var result = await _controller.ListGames(query, CancellationToken.None);

        // Assert
        var errorResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}

