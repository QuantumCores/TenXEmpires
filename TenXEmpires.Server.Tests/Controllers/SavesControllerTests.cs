using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Controllers;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Domain.Constants;

namespace TenXEmpires.Server.Tests.Controllers;

/// <summary>
/// Tests for SavesController focusing on HTTP concerns:
/// - Authentication and authorization
/// - HTTP response codes and formats
/// - Error handling at the controller boundary
/// 
/// Business logic tests are in SaveServiceTests.
/// </summary>
public class SavesControllerTests
{
    private readonly Mock<ISaveService> _saveServiceMock;
    private readonly Mock<IGameService> _gameServiceMock;
    private readonly Mock<ILogger<SavesController>> _loggerMock;
    private readonly SavesController _controller;
    private readonly Guid _testUserId;

    public SavesControllerTests()
    {
        _saveServiceMock = new Mock<ISaveService>();
        _gameServiceMock = new Mock<IGameService>();
        _loggerMock = new Mock<ILogger<SavesController>>();
        _testUserId = Guid.NewGuid();
        
        _controller = new SavesController(
            _saveServiceMock.Object,
            _gameServiceMock.Object,
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
    public async Task CreateManualSave_WithValidRequest_ShouldReturn201()
    {
        // Arrange
        var gameId = 42L;
        var command = new CreateManualSaveCommand(1, "Before assault on capital");
        var expected = new SaveCreatedDto(301, 1, 8, DateTimeOffset.UtcNow, command.Name);

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _saveServiceMock
            .Setup(s => s.CreateManualAsync(_testUserId, gameId, command, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Add idempotency header
        _controller.ControllerContext.HttpContext.Request.Headers[TenxHeaders.IdempotencyKey] = "req-1";

        // Act
        var result = await _controller.CreateManualSave(gameId, command, CancellationToken.None);

        // Assert
        var created = result.Result.Should().BeOfType<CreatedAtRouteResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.RouteName.Should().Be("ListGameSaves");
        created.Value.Should().BeOfType<SaveCreatedDto>();
    }

    [Fact]
    public async Task CreateManualSave_WithInvalidSlot_ShouldReturn400()
    {
        // Arrange
        var gameId = 42L;
        var command = new CreateManualSaveCommand(0, "name");

        // Act
        var result = await _controller.CreateManualSave(gameId, command, CancellationToken.None);

        // Assert
        var badReq = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badReq.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateManualSave_WithEmptyName_ShouldReturn400()
    {
        // Arrange
        var gameId = 42L;
        var command = new CreateManualSaveCommand(1, "  ");

        // Act
        var result = await _controller.CreateManualSave(gameId, command, CancellationToken.None);

        // Assert
        var badReq = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badReq.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateManualSave_WithNoAccess_ShouldReturn404()
    {
        // Arrange
        var gameId = 42L;
        var command = new CreateManualSaveCommand(1, "name");

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.CreateManualSave(gameId, command, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateManualSave_WithServiceConflict_ShouldReturn409()
    {
        // Arrange
        var gameId = 42L;
        var command = new CreateManualSaveCommand(1, "name");

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _saveServiceMock
            .Setup(s => s.CreateManualAsync(_testUserId, gameId, command, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SAVE_CONFLICT: upsert failed"));

        // Act
        var result = await _controller.CreateManualSave(gameId, command, CancellationToken.None);

        // Assert
        var conflict = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task ListGameSaves_WithValidGameAccess_ShouldReturn200WithSavesList()
    {
        // Arrange
        var gameId = 42L;
        var manualSaves = new List<SaveManualDto>
        {
            new(Id: 1, Slot: 1, TurnNo: 5, CreatedAt: DateTimeOffset.UtcNow, Name: "Before battle")
        };
        var autosaves = new List<SaveAutosaveDto>
        {
            new(Id: 10, TurnNo: 6, CreatedAt: DateTimeOffset.UtcNow)
        };
        var expectedResult = new GameSavesListDto(manualSaves, autosaves);

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _saveServiceMock
            .Setup(s => s.ListSavesAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.ListGameSaves(gameId, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        
        var returnedData = ok.Value.Should().BeOfType<GameSavesListDto>().Subject;
        returnedData.Manual.Should().HaveCount(1);
        returnedData.Manual[0].Name.Should().Be("Before battle");
        returnedData.Autosaves.Should().HaveCount(1);
        returnedData.Autosaves[0].TurnNo.Should().Be(6);

        _gameServiceMock.Verify(
            s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()),
            Times.Once);
        _saveServiceMock.Verify(
            s => s.ListSavesAsync(gameId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListGameSaves_WithNoSaves_ShouldReturn200WithEmptyLists()
    {
        // Arrange
        var gameId = 42L;
        var expectedResult = new GameSavesListDto(
            new List<SaveManualDto>(),
            new List<SaveAutosaveDto>());

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _saveServiceMock
            .Setup(s => s.ListSavesAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.ListGameSaves(gameId, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        
        var returnedData = ok.Value.Should().BeOfType<GameSavesListDto>().Subject;
        returnedData.Manual.Should().BeEmpty();
        returnedData.Autosaves.Should().BeEmpty();
    }

    [Fact]
    public async Task ListGameSaves_WithGameNotFound_ShouldReturn404()
    {
        // Arrange
        var gameId = 999L;

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.ListGameSaves(gameId, CancellationToken.None);

        // Assert
        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        notFound.Value.Should().NotBeNull();

        _gameServiceMock.Verify(
            s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()),
            Times.Once);
        _saveServiceMock.Verify(
            s => s.ListSavesAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ListGameSaves_WithUserNoAccess_ShouldReturn404()
    {
        // Arrange
        var gameId = 42L;

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.ListGameSaves(gameId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        
        _saveServiceMock.Verify(
            s => s.ListSavesAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Service should not be called if game access verification fails");
    }

    [Fact]
    public async Task ListGameSaves_WithUnauthorizedException_ShouldReturn401()
    {
        // Arrange
        var gameId = 42L;

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("User not authenticated"));

        // Act
        var result = await _controller.ListGameSaves(gameId, CancellationToken.None);

        // Assert
        var unauthorized = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        unauthorized.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ListGameSaves_WithServiceException_ShouldReturn500()
    {
        // Arrange
        var gameId = 42L;

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _saveServiceMock
            .Setup(s => s.ListSavesAsync(gameId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _controller.ListGameSaves(gameId, CancellationToken.None);

        // Assert
        var serverError = result.Result.Should().BeOfType<ObjectResult>().Subject;
        serverError.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        serverError.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ListGameSaves_WithMixedSaves_ShouldReturnBothGroups()
    {
        // Arrange
        var gameId = 42L;
        var manualSaves = new List<SaveManualDto>
        {
            new(Id: 1, Slot: 1, TurnNo: 5, CreatedAt: DateTimeOffset.UtcNow.AddHours(-2), Name: "Strategic save"),
            new(Id: 2, Slot: 2, TurnNo: 10, CreatedAt: DateTimeOffset.UtcNow.AddHours(-1), Name: "Before attack")
        };
        var autosaves = new List<SaveAutosaveDto>
        {
            new(Id: 10, TurnNo: 12, CreatedAt: DateTimeOffset.UtcNow),
            new(Id: 11, TurnNo: 11, CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-30)),
            new(Id: 12, TurnNo: 10, CreatedAt: DateTimeOffset.UtcNow.AddHours(-1))
        };
        var expectedResult = new GameSavesListDto(manualSaves, autosaves);

        _gameServiceMock
            .Setup(s => s.VerifyGameAccessAsync(_testUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _saveServiceMock
            .Setup(s => s.ListSavesAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.ListGameSaves(gameId, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedData = ok.Value.Should().BeOfType<GameSavesListDto>().Subject;
        
        returnedData.Manual.Should().HaveCount(2);
        returnedData.Manual[0].Slot.Should().Be(1);
        returnedData.Manual[1].Slot.Should().Be(2);
        
        returnedData.Autosaves.Should().HaveCount(3);
        returnedData.Autosaves[0].TurnNo.Should().Be(12);
        returnedData.Autosaves[1].TurnNo.Should().Be(11);
        returnedData.Autosaves[2].TurnNo.Should().Be(10);
    }
}

