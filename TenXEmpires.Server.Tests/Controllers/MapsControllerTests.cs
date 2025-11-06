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

public class MapsControllerTests
{
    private readonly Mock<ILookupService> _lookupServiceMock;
    private readonly Mock<ILogger<MapsController>> _loggerMock;
    private readonly MapsController _controller;

    public MapsControllerTests()
    {
        _lookupServiceMock = new Mock<ILookupService>();
        _loggerMock = new Mock<ILogger<MapsController>>();
        _controller = new MapsController(_lookupServiceMock.Object, _loggerMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task GetMapByCode_ShouldReturnBadRequest_WhenCodeMissing()
    {
        var result = await _controller.GetMapByCode("");

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetMapByCode_ShouldReturnNotFound_WhenMapDoesNotExist()
    {
        _lookupServiceMock.Setup(s => s.GetMapByCodeAsync("unknown")).ReturnsAsync((MapDto?)null);

        var result = await _controller.GetMapByCode("unknown");

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetMapByCode_ShouldReturnOk_WithMapDto()
    {
        var map = new MapDto(1, "map-01", 1, 10, 10);
        _lookupServiceMock.Setup(s => s.GetMapByCodeAsync("map-01")).ReturnsAsync(map);
        _lookupServiceMock.Setup(s => s.ComputeMapETag(map)).Returns(GetEtag(map));

        var result = await _controller.GetMapByCode("map-01");

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<MapDto>();
        _controller.Response.Headers.CacheControl.ToString().Should().Contain("max-age=600");
        _controller.Response.Headers.ETag.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetMapByCode_WhenETagMatches_ShouldReturn304()
    {
        var map = new MapDto(1, "map-01", 1, 20, 30);
        _lookupServiceMock.Setup(s => s.GetMapByCodeAsync("map-01")).ReturnsAsync(map);

        // Compute expected ETag using same method as service
        var etag = GetEtag(map);
        _lookupServiceMock.Setup(s => s.ComputeMapETag(map)).Returns(etag);
        _controller.Request.Headers.IfNoneMatch = new StringValues(etag);

        var result = await _controller.GetMapByCode("map-01");

        result.Result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status304NotModified);
        _controller.Response.Headers.ETag.ToString().Should().Be(etag);
    }

    [Fact]
    public async Task GetMapByCode_WhenETagDiffers_ShouldReturnOk()
    {
        var map = new MapDto(1, "map-01", 1, 20, 30);
        _lookupServiceMock.Setup(s => s.GetMapByCodeAsync("map-01")).ReturnsAsync(map);
        _lookupServiceMock.Setup(s => s.ComputeMapETag(map)).Returns(GetEtag(map));

        _controller.Request.Headers.IfNoneMatch = new StringValues("\"some-other\"");

        var result = await _controller.GetMapByCode("map-01");

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    #region GetMapTiles Tests

    [Fact]
    public async Task GetMapTiles_ShouldReturnBadRequest_WhenCodeMissing()
    {
        var result = await _controller.GetMapTiles("");

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetMapTiles_ShouldReturnBadRequest_WhenPageInvalid()
    {
        var result = await _controller.GetMapTiles("test-map", page: 0);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetMapTiles_ShouldReturnBadRequest_WhenPageSizeInvalid()
    {
        var result1 = await _controller.GetMapTiles("test-map", pageSize: 0);
        var result2 = await _controller.GetMapTiles("test-map", pageSize: 1001);

        result1.Result.Should().BeOfType<BadRequestObjectResult>();
        result2.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetMapTiles_ShouldReturnNotFound_WhenMapDoesNotExist()
    {
        _lookupServiceMock.Setup(s => s.GetMapTilesAsync("unknown", It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync((PagedResult<MapTileDto>?)null);

        var result = await _controller.GetMapTiles("unknown");

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetMapTiles_ShouldReturnOk_WithPagedResult()
    {
        var tiles = new List<MapTileDto>
        {
            new MapTileDto(1, 0, 0, "grassland", "wheat", 2),
            new MapTileDto(2, 0, 1, "plains", null, 0),
            new MapTileDto(3, 1, 0, "hills", "iron", 3)
        };
        var pagedResult = new PagedResult<MapTileDto>
        {
            Items = tiles,
            Page = 1,
            PageSize = 500,
            Total = 3
        };

        _lookupServiceMock.Setup(s => s.GetMapTilesAsync("test-map", 1, 500))
            .ReturnsAsync(pagedResult);
        _lookupServiceMock.Setup(s => s.GetMapTilesETagAsync("test-map", 1, 500))
            .ReturnsAsync("\"test-etag\"");

        var result = await _controller.GetMapTiles("test-map");

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value.Should().BeOfType<PagedResult<MapTileDto>>().Subject;
        value.Items.Should().HaveCount(3);
        value.Page.Should().Be(1);
        value.PageSize.Should().Be(500);
        value.Total.Should().Be(3);
        _controller.Response.Headers.CacheControl.ToString().Should().Contain("max-age=600");
        _controller.Response.Headers.ETag.ToString().Should().Be("\"test-etag\"");
    }

    [Fact]
    public async Task GetMapTiles_ShouldApplyPaginationParameters()
    {
        var tiles = new List<MapTileDto>
        {
            new MapTileDto(11, 1, 0, "grassland", null, 0),
            new MapTileDto(12, 1, 1, "plains", null, 0)
        };
        var pagedResult = new PagedResult<MapTileDto>
        {
            Items = tiles,
            Page = 2,
            PageSize = 10,
            Total = null
        };

        _lookupServiceMock.Setup(s => s.GetMapTilesAsync("test-map", 2, 10))
            .ReturnsAsync(pagedResult);
        _lookupServiceMock.Setup(s => s.GetMapTilesETagAsync("test-map", 2, 10))
            .ReturnsAsync("\"test-etag-page2\"");

        var result = await _controller.GetMapTiles("test-map", page: 2, pageSize: 10);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value.Should().BeOfType<PagedResult<MapTileDto>>().Subject;
        value.Page.Should().Be(2);
        value.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetMapTiles_WhenETagMatches_ShouldReturn304()
    {
        var tiles = new List<MapTileDto>
        {
            new MapTileDto(1, 0, 0, "grassland", null, 0)
        };
        var pagedResult = new PagedResult<MapTileDto>
        {
            Items = tiles,
            Page = 1,
            PageSize = 500,
            Total = 1
        };

        _lookupServiceMock.Setup(s => s.GetMapTilesAsync("test-map", 1, 500))
            .ReturnsAsync(pagedResult);
        _lookupServiceMock.Setup(s => s.GetMapTilesETagAsync("test-map", 1, 500))
            .ReturnsAsync("\"matching-etag\"");

        _controller.Request.Headers.IfNoneMatch = new StringValues("\"matching-etag\"");

        var result = await _controller.GetMapTiles("test-map");

        result.Result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status304NotModified);
        _controller.Response.Headers.ETag.ToString().Should().Be("\"matching-etag\"");
    }

    [Fact]
    public async Task GetMapTiles_WhenETagDiffers_ShouldReturnOk()
    {
        var tiles = new List<MapTileDto>
        {
            new MapTileDto(1, 0, 0, "grassland", null, 0)
        };
        var pagedResult = new PagedResult<MapTileDto>
        {
            Items = tiles,
            Page = 1,
            PageSize = 500,
            Total = 1
        };

        _lookupServiceMock.Setup(s => s.GetMapTilesAsync("test-map", 1, 500))
            .ReturnsAsync(pagedResult);
        _lookupServiceMock.Setup(s => s.GetMapTilesETagAsync("test-map", 1, 500))
            .ReturnsAsync("\"current-etag\"");

        _controller.Request.Headers.IfNoneMatch = new StringValues("\"old-etag\"");

        var result = await _controller.GetMapTiles("test-map");

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMapTiles_ShouldHandleNullETag()
    {
        var tiles = new List<MapTileDto>
        {
            new MapTileDto(1, 0, 0, "grassland", null, 0)
        };
        var pagedResult = new PagedResult<MapTileDto>
        {
            Items = tiles,
            Page = 1,
            PageSize = 500,
            Total = 1
        };

        _lookupServiceMock.Setup(s => s.GetMapTilesAsync("test-map", 1, 500))
            .ReturnsAsync(pagedResult);
        _lookupServiceMock.Setup(s => s.GetMapTilesETagAsync("test-map", 1, 500))
            .ReturnsAsync((string?)null);

        var result = await _controller.GetMapTiles("test-map");

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        _controller.Response.Headers.ETag.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task GetMapTiles_ShouldHandleServiceArgumentException()
    {
        _lookupServiceMock.Setup(s => s.GetMapTilesAsync("test-map", It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new ArgumentException("Invalid parameter", "page"));

        var result = await _controller.GetMapTiles("test-map");

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    private static string GetEtag(MapDto map)
    {
        var representation = $"{map.Code}:{map.SchemaVersion}:{map.Width}:{map.Height}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(representation);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        var etag = Convert.ToBase64String(hash);
        return $"\"{etag}\"";
    }
}

