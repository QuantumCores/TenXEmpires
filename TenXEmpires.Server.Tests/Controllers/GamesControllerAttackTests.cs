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

public class GamesControllerAttackTests
{
    private readonly Mock<IGameService> _gameServiceMock = new();
    private readonly Mock<IGameStateService> _gameStateServiceMock = new();
    private readonly Mock<ITurnService> _turnServiceMock = new();
    private readonly Mock<IActionService> _actionServiceMock = new();
    private readonly Mock<ILogger<GamesController>> _loggerMock = new();

    private readonly GamesController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public GamesControllerAttackTests()
    {
        _controller = new GamesController(
            _gameServiceMock.Object,
            _gameStateServiceMock.Object,
            _turnServiceMock.Object,
            _actionServiceMock.Object,
            _loggerMock.Object);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Name, "testuser@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task AttackUnit_Success_ShouldReturn200()
    {
        // Arrange
        var gameId = 42L;
        var command = new AttackUnitCommand(201, 305);
        var state = new GameStateDto(
            new GameStateGameDto(gameId, 1, 1, false, "active"),
            new GameStateMapDto(1, "code", 1, 8, 6),
            new List<ParticipantDto>(), new List<UnitInStateDto>(), new List<CityInStateDto>(),
            new List<CityTileLinkDto>(), new List<CityResourceDto>(), new List<UnitDefinitionDto>(), null);
        var expected = new ActionStateResponse(state);

        _actionServiceMock
            .Setup(s => s.AttackAsync(_userId, gameId, command, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.AttackUnit(gameId, command, CancellationToken.None);

        // Assert
        var ok = result.Result.As<OkObjectResult>();
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().BeOfType<ActionStateResponse>();
    }

    [Fact]
    public async Task AttackUnit_NotPlayerTurn_ShouldReturn409()
    {
        // Arrange
        var gameId = 42L;
        var command = new AttackUnitCommand(201, 305);
        _actionServiceMock
            .Setup(s => s.AttackAsync(_userId, gameId, command, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("NOT_PLAYER_TURN: It is not your turn."));

        // Act
        var result = await _controller.AttackUnit(gameId, command, CancellationToken.None);

        // Assert
        var conflict = result.Result.As<ConflictObjectResult>();
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task AttackUnit_OutOfRange_ShouldReturn422()
    {
        // Arrange
        var gameId = 42L;
        var command = new AttackUnitCommand(201, 999);
        _actionServiceMock
            .Setup(s => s.AttackAsync(_userId, gameId, command, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("OUT_OF_RANGE: Ranged attack distance 3 not in [1,2]."));

        // Act
        var result = await _controller.AttackUnit(gameId, command, CancellationToken.None);

        // Assert
        var unproc = result.Result.As<UnprocessableEntityObjectResult>();
        unproc.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task AttackUnit_InvalidTarget_ShouldReturn422()
    {
        // Arrange
        var gameId = 42L;
        var command = new AttackUnitCommand(201, 201); // same owner - invalid target
        _actionServiceMock
            .Setup(s => s.AttackAsync(_userId, gameId, command, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("INVALID_TARGET: Target must be an enemy unit."));

        // Act
        var result = await _controller.AttackUnit(gameId, command, CancellationToken.None);

        // Assert
        var unproc = result.Result.As<UnprocessableEntityObjectResult>();
        unproc.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task AttackUnit_UnitNotFound_ShouldReturn404()
    {
        // Arrange
        var gameId = 42L;
        var command = new AttackUnitCommand(201, 999);
        _actionServiceMock
            .Setup(s => s.AttackAsync(_userId, gameId, command, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("UNIT_NOT_FOUND: Target unit not found."));

        // Act
        var result = await _controller.AttackUnit(gameId, command, CancellationToken.None);

        // Assert
        var notFound = result.Result.As<NotFoundObjectResult>();
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
