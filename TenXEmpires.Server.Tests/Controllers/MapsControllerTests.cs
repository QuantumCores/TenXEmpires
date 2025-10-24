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

        // Compute expected ETag using same method as controller
        var etag = GetEtag(map);
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

        _controller.Request.Headers.IfNoneMatch = new StringValues("\"some-other\"");

        var result = await _controller.GetMapByCode("map-01");

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    private static string GetEtag(MapDto map)
    {
        var representation = $"{map.Code}:{map.SchemaVersion}:{map.Width}:{map.Height}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(representation);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        var etag = Convert.ToBase64String(hash);
        return $"\"{etag}\"";
    }
}

