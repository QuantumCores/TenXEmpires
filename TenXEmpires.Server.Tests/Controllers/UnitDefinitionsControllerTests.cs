using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using TenXEmpires.Server.Controllers;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;

namespace TenXEmpires.Server.Tests.Controllers;

public class UnitDefinitionsControllerTests
{
    private readonly Mock<ILookupService> _lookupServiceMock;
    private readonly Mock<ILogger<UnitDefinitionsController>> _loggerMock;
    private readonly UnitDefinitionsController _controller;

    public UnitDefinitionsControllerTests()
    {
        _lookupServiceMock = new Mock<ILookupService>();
        _loggerMock = new Mock<ILogger<UnitDefinitionsController>>();
        _controller = new UnitDefinitionsController(_lookupServiceMock.Object, _loggerMock.Object);

        // Setup HttpContext for the controller
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task GetUnitDefinitions_ShouldReturnOkWithData()
    {
        // Arrange
        var unitDefinitions = new List<UnitDefinitionDto>
        {
            new(1, "warrior", false, 20, 10, 0, 0, 2, 100),
            new(2, "archer", true, 15, 5, 2, 3, 2, 80)
        };

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsAsync())
            .ReturnsAsync(unitDefinitions);

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsETagAsync())
            .ReturnsAsync("\"test-etag-123\"");

        // Act
        var result = await _controller.GetUnitDefinitions();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ItemsResult<UnitDefinitionDto>>().Subject;
        
        response.Items.Should().HaveCount(2);
        response.Items.Should().Contain(u => u.Code == "warrior");
        response.Items.Should().Contain(u => u.Code == "archer");
    }

    [Fact]
    public async Task GetUnitDefinitions_ShouldSetETagHeader()
    {
        // Arrange
        var unitDefinitions = new List<UnitDefinitionDto>
        {
            new(1, "warrior", false, 20, 10, 0, 0, 2, 100)
        };
        var expectedETag = "\"test-etag-123\"";

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsAsync())
            .ReturnsAsync(unitDefinitions);

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsETagAsync())
            .ReturnsAsync(expectedETag);

        // Act
        await _controller.GetUnitDefinitions();

        // Assert
        _controller.Response.Headers.ETag.ToString().Should().Be(expectedETag);
    }

    [Fact]
    public async Task GetUnitDefinitions_ShouldSetCacheControlHeader()
    {
        // Arrange
        var unitDefinitions = new List<UnitDefinitionDto>
        {
            new(1, "warrior", false, 20, 10, 0, 0, 2, 100)
        };

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsAsync())
            .ReturnsAsync(unitDefinitions);

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsETagAsync())
            .ReturnsAsync("\"test-etag\"");

        // Act
        await _controller.GetUnitDefinitions();

        // Assert
        _controller.Response.Headers.CacheControl.ToString().Should().Contain("public");
        _controller.Response.Headers.CacheControl.ToString().Should().Contain("max-age=600");
    }

    [Fact]
    public async Task GetUnitDefinitions_WhenETagMatches_ShouldReturn304()
    {
        // Arrange
        var currentETag = "\"test-etag-123\"";
        
        _controller.Request.Headers.IfNoneMatch = new StringValues(currentETag);

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsETagAsync())
            .ReturnsAsync(currentETag);

        // Act
        var result = await _controller.GetUnitDefinitions();

        // Assert
        result.Result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status304NotModified);

        // Service should not fetch data when returning 304
        _lookupServiceMock.Verify(s => s.GetUnitDefinitionsAsync(), Times.Never);
    }

    [Fact]
    public async Task GetUnitDefinitions_WhenETagDoesNotMatch_ShouldReturnData()
    {
        // Arrange
        var clientETag = "\"old-etag\"";
        var currentETag = "\"new-etag\"";
        var unitDefinitions = new List<UnitDefinitionDto>
        {
            new(1, "warrior", false, 20, 10, 0, 0, 2, 100)
        };

        _controller.Request.Headers.IfNoneMatch = new StringValues(clientETag);

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsETagAsync())
            .ReturnsAsync(currentETag);

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsAsync())
            .ReturnsAsync(unitDefinitions);

        // Act
        var result = await _controller.GetUnitDefinitions();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ItemsResult<UnitDefinitionDto>>().Subject;
        
        response.Items.Should().HaveCount(1);
        _lookupServiceMock.Verify(s => s.GetUnitDefinitionsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetUnitDefinitions_WhenNoIfNoneMatchHeader_ShouldReturnData()
    {
        // Arrange
        var unitDefinitions = new List<UnitDefinitionDto>
        {
            new(1, "warrior", false, 20, 10, 0, 0, 2, 100)
        };

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsETagAsync())
            .ReturnsAsync("\"test-etag\"");

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsAsync())
            .ReturnsAsync(unitDefinitions);

        // Act
        var result = await _controller.GetUnitDefinitions();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _lookupServiceMock.Verify(s => s.GetUnitDefinitionsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetUnitDefinitions_WhenServiceThrows_ShouldReturn500()
    {
        // Arrange
        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsETagAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetUnitDefinitions();

        // Assert
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        
        var errorResponse = statusResult.Value;
        errorResponse.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUnitDefinitions_ShouldReturnEmptyListWhenNoData()
    {
        // Arrange
        var emptyList = new List<UnitDefinitionDto>();

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsAsync())
            .ReturnsAsync(emptyList);

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsETagAsync())
            .ReturnsAsync("\"empty-etag\"");

        // Act
        var result = await _controller.GetUnitDefinitions();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ItemsResult<UnitDefinitionDto>>().Subject;
        
        response.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUnitDefinitions_ShouldCallServiceMethods()
    {
        // Arrange
        var unitDefinitions = new List<UnitDefinitionDto>
        {
            new(1, "warrior", false, 20, 10, 0, 0, 2, 100)
        };

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsAsync())
            .ReturnsAsync(unitDefinitions);

        _lookupServiceMock
            .Setup(s => s.GetUnitDefinitionsETagAsync())
            .ReturnsAsync("\"test-etag\"");

        // Act
        await _controller.GetUnitDefinitions();

        // Assert
        _lookupServiceMock.Verify(s => s.GetUnitDefinitionsETagAsync(), Times.Once);
        _lookupServiceMock.Verify(s => s.GetUnitDefinitionsAsync(), Times.Once);
    }
}

