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
    private readonly Mock<ITurnService> _turnServiceMock;
    private readonly Mock<IActionService> _actionServiceMock;
    private readonly Mock<ILogger<GamesController>> _loggerMock;
    private readonly GamesController _controller;
    private readonly Guid _testUserId;

    public GamesControllerTests()
    {
        _gameServiceMock = new Mock<IGameService>();
        _gameStateServiceMock = new Mock<IGameStateService>();
        _turnServiceMock = new Mock<ITurnService>();
        _actionServiceMock = new Mock<IActionService>();
        _loggerMock = new Mock<ILogger<GamesController>>();
        _testUserId = Guid.NewGuid();
        
        _controller = new GamesController(
            _gameServiceMock.Object,
            _gameStateServiceMock.Object,
            _turnServiceMock.Object,
            _actionServiceMock.Object,
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
            _turnServiceMock.Object,
            _actionServiceMock.Object,
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
            _turnServiceMock.Object,
            _actionServiceMock.Object,
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

    #region GetGameState Tests

    [Fact]
    public async Task GetGameState_WithValidAccess_ShouldReturn200WithGameState()
    {
        // Arrange
        var gameId = 42L;
        var gameState = CreateSampleGameState(gameId);

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _gameStateServiceMock
            .Setup(s => s.BuildGameStateAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameState);

        // Act
        var result = await _controller.GetGameState(gameId, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeOfType<GameStateDto>();
        
        var returnedState = okResult.Value as GameStateDto;
        returnedState.Should().NotBeNull();
        returnedState!.Game.Id.Should().Be(gameId);
    }

    [Fact]
    public async Task GetGameState_WithNonExistentGame_ShouldReturn404()
    {
        // Arrange
        var gameId = 999L;

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.GetGameState(gameId, CancellationToken.None);

        // Assert
        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        
        _gameStateServiceMock.Verify(
            s => s.BuildGameStateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetGameState_WithGameOwnedByAnotherUser_ShouldReturn404()
    {
        // Arrange
        var gameId = 42L;

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.GetGameState(gameId, CancellationToken.None);

        // Assert
        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetGameState_WhenServiceThrowsInvalidOperationException_ShouldReturn404()
    {
        // Arrange
        var gameId = 42L;

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _gameStateServiceMock
            .Setup(s => s.BuildGameStateAsync(gameId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Game not found"));

        // Act
        var result = await _controller.GetGameState(gameId, CancellationToken.None);

        // Assert
        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetGameState_WhenServiceThrowsUnauthorizedException_ShouldReturn401()
    {
        // Arrange
        var gameId = 42L;

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _gameStateServiceMock
            .Setup(s => s.BuildGameStateAsync(gameId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

        // Act
        var result = await _controller.GetGameState(gameId, CancellationToken.None);

        // Assert
        var unauthorized = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetGameState_WhenServiceThrowsException_ShouldReturn500()
    {
        // Arrange
        var gameId = 42L;

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _gameStateServiceMock
            .Setup(s => s.BuildGameStateAsync(gameId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetGameState(gameId, CancellationToken.None);

        // Assert
        var errorResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task GetGameState_ShouldPassCorrectUserIdToService()
    {
        // Arrange
        var gameId = 42L;
        var gameState = CreateSampleGameState(gameId);

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _gameStateServiceMock
            .Setup(s => s.BuildGameStateAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameState);

        // Act
        await _controller.GetGameState(gameId, CancellationToken.None);

        // Assert
        _gameServiceMock.Verify(
            s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetGameState_ShouldCallBuildGameStateAfterVerification()
    {
        // Arrange
        var gameId = 42L;
        var gameState = CreateSampleGameState(gameId);

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _gameStateServiceMock
            .Setup(s => s.BuildGameStateAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameState);

        // Act
        await _controller.GetGameState(gameId, CancellationToken.None);

        // Assert
        _gameStateServiceMock.Verify(
            s => s.BuildGameStateAsync(gameId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static GameStateDto CreateSampleGameState(long gameId)
    {
        var game = new GameStateGameDto(
            Id: gameId,
            TurnNo: 1,
            ActiveParticipantId: 101,
            TurnInProgress: false,
            Status: "active");

        var map = new GameStateMapDto(
            Id: 1,
            Code: "standard_15x20",
            SchemaVersion: 1,
            Width: 8,
            Height: 6);

        var participants = new List<ParticipantDto>
        {
            new ParticipantDto(101, gameId, "human", Guid.NewGuid(), "Player", false)
        };

        return new GameStateDto(
            game,
            map,
            participants,
            new List<UnitInStateDto>(),
            new List<CityInStateDto>(),
            new List<CityTileLinkDto>(),
            new List<CityResourceDto>(),
            new List<UnitDefinitionDto>(),
            null);
    }

    #endregion

    #region DeleteGame Tests

    [Fact]
    public async Task DeleteGame_WithValidOwner_ShouldReturn204()
    {
        // Arrange
        var gameId = 42L;
        _gameServiceMock
            .Setup(s => s.DeleteGameAsync(_testUserId, gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteGame(gameId, CancellationToken.None);

        // Assert
        var noContent = result.Should().BeOfType<NoContentResult>().Subject;
        noContent.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task DeleteGame_WithNonExistentGame_ShouldReturn404()
    {
        // Arrange
        var gameId = 999L;
        _gameServiceMock
            .Setup(s => s.DeleteGameAsync(_testUserId, gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteGame(gameId, CancellationToken.None);

        // Assert
        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DeleteGame_WithGameOwnedByAnotherUser_ShouldReturn404()
    {
        // Arrange
        var gameId = 42L;
        _gameServiceMock
            .Setup(s => s.DeleteGameAsync(_testUserId, gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Service returns false for unauthorized access

        // Act
        var result = await _controller.DeleteGame(gameId, CancellationToken.None);

        // Assert
        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DeleteGame_WithIdempotencyKey_ShouldPassToService()
    {
        // Arrange
        var gameId = 42L;
        var idempotencyKey = "test-key-123";
        _controller.Request.Headers["X-Tenx-Idempotency-Key"] = idempotencyKey;

        _gameServiceMock
            .Setup(s => s.DeleteGameAsync(_testUserId, gameId, idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteGame(gameId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _gameServiceMock.Verify(
            s => s.DeleteGameAsync(_testUserId, gameId, idempotencyKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteGame_WhenServiceThrowsUnauthorizedException_ShouldReturn401()
    {
        // Arrange
        var gameId = 42L;
        _gameServiceMock
            .Setup(s => s.DeleteGameAsync(_testUserId, gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Unauthorized"));

        // Act
        var result = await _controller.DeleteGame(gameId, CancellationToken.None);

        // Assert
        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task DeleteGame_WhenServiceThrowsException_ShouldReturn500()
    {
        // Arrange
        var gameId = 42L;
        _gameServiceMock
            .Setup(s => s.DeleteGameAsync(_testUserId, gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.DeleteGame(gameId, CancellationToken.None);

        // Assert
        var errorResult = result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task DeleteGame_ShouldPassCorrectUserIdToService()
    {
        // Arrange
        var gameId = 42L;
        _gameServiceMock
            .Setup(s => s.DeleteGameAsync(_testUserId, gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _controller.DeleteGame(gameId, CancellationToken.None);

        // Assert
        _gameServiceMock.Verify(
            s => s.DeleteGameAsync(_testUserId, gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ListGameTurns Tests

    [Fact]
    public async Task ListGameTurns_WithValidRequest_ShouldReturn200AndPagedResult()
    {
        // Arrange
        var gameId = 42L;
        var query = new ListTurnsQuery { Page = 1, PageSize = 20 };
        var expectedResult = new PagedResult<TurnDto>
        {
            Items = new List<TurnDto>
            {
                new TurnDto(1, 5, 1, DateTimeOffset.UtcNow, 10000, null),
                new TurnDto(2, 4, 2, DateTimeOffset.UtcNow.AddHours(-1), 8000, null)
            },
            Page = 1,
            PageSize = 20,
            Total = 2
        };

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _turnServiceMock
            .Setup(s => s.ListTurnsAsync(gameId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.ListGameTurns(gameId, query, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        var pagedResult = ok.Value.Should().BeOfType<PagedResult<TurnDto>>().Subject;
        pagedResult.Items.Should().HaveCount(2);
        pagedResult.Total.Should().Be(2);
    }

    [Fact]
    public async Task ListGameTurns_WithNonExistentGame_ShouldReturn404()
    {
        // Arrange
        var gameId = 999L;
        var query = new ListTurnsQuery();

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.ListGameTurns(gameId, query, CancellationToken.None);

        // Assert
        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ListGameTurns_WithGameOwnedByDifferentUser_ShouldReturn404()
    {
        // Arrange
        var gameId = 42L;
        var query = new ListTurnsQuery();

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.ListGameTurns(gameId, query, CancellationToken.None);

        // Assert
        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ListGameTurns_WithInvalidSortField_ShouldReturn400()
    {
        // Arrange
        var gameId = 42L;
        var query = new ListTurnsQuery { Sort = "invalidField" };

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _turnServiceMock
            .Setup(s => s.ListTurnsAsync(gameId, query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid sort field", "Sort"));

        // Act
        var result = await _controller.ListGameTurns(gameId, query, CancellationToken.None);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListGameTurns_WithInvalidOrder_ShouldReturn400()
    {
        // Arrange
        var gameId = 42L;
        var query = new ListTurnsQuery { Order = "sideways" };

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _turnServiceMock
            .Setup(s => s.ListTurnsAsync(gameId, query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid order", "Order"));

        // Act
        var result = await _controller.ListGameTurns(gameId, query, CancellationToken.None);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListGameTurns_WithEmptyResult_ShouldReturn200WithEmptyItems()
    {
        // Arrange
        var gameId = 42L;
        var query = new ListTurnsQuery();
        var emptyResult = new PagedResult<TurnDto>
        {
            Items = new List<TurnDto>(),
            Page = 1,
            PageSize = 20,
            Total = 0
        };

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _turnServiceMock
            .Setup(s => s.ListTurnsAsync(gameId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);

        // Act
        var result = await _controller.ListGameTurns(gameId, query, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        var pagedResult = ok.Value.Should().BeOfType<PagedResult<TurnDto>>().Subject;
        pagedResult.Items.Should().BeEmpty();
        pagedResult.Total.Should().Be(0);
    }

    [Fact]
    public async Task ListGameTurns_WithServiceException_ShouldReturn500()
    {
        // Arrange
        var gameId = 42L;
        var query = new ListTurnsQuery();

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _turnServiceMock
            .Setup(s => s.ListTurnsAsync(gameId, query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _controller.ListGameTurns(gameId, query, CancellationToken.None);

        // Assert
        var serverError = result.Result.Should().BeOfType<ObjectResult>().Subject;
        serverError.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task ListGameTurns_WithPaginationParameters_ShouldPassThroughCorrectly()
    {
        // Arrange
        var gameId = 42L;
        var query = new ListTurnsQuery { Page = 2, PageSize = 50, Sort = "committedAt", Order = "asc" };
        var expectedResult = new PagedResult<TurnDto>
        {
            Items = new List<TurnDto>(),
            Page = 2,
            PageSize = 50,
            Total = 100
        };

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _turnServiceMock
            .Setup(s => s.ListTurnsAsync(gameId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.ListGameTurns(gameId, query, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = ok.Value.Should().BeOfType<PagedResult<TurnDto>>().Subject;
        pagedResult.Page.Should().Be(2);
        pagedResult.PageSize.Should().Be(50);
        pagedResult.Total.Should().Be(100);

        _turnServiceMock.Verify(
            s => s.ListTurnsAsync(gameId, query, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}

