using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Controllers;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;

namespace TenXEmpires.Server.Tests.Controllers;

public class AnalyticsControllerTests
{
    private readonly Mock<IAnalyticsService> _service;
    private readonly Mock<ILogger<AnalyticsController>> _logger;
    private readonly AnalyticsController _controller;

    public AnalyticsControllerTests()
    {
        _service = new Mock<IAnalyticsService>();
        _logger = new Mock<ILogger<AnalyticsController>>();
        _controller = new AnalyticsController(_service.Object, _logger.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task IngestBatch_WithValidEvents_ShouldReturn202WithCount()
    {
        // Arrange
        var command = new AnalyticsBatchCommand(new List<AnalyticsEventItem>
        {
            new("turn_end", 42, 5, DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), null),
            new("autosave", 42, 5, DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), null)
        });

        _service.Setup(s => s.IngestBatchAsync(null, It.IsAny<string?>(), command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        var result = await _controller.IngestBatch(command, CancellationToken.None);

        // Assert
        var accepted = result.Result.Should().BeOfType<AcceptedResult>().Subject;
        accepted.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        accepted.Value.Should().BeOfType<AnalyticsBatchResponse>().Which.Accepted.Should().Be(2);
    }

    [Fact]
    public async Task IngestBatch_WithInvalidEventType_ShouldReturn400()
    {
        var command = new AnalyticsBatchCommand(new List<AnalyticsEventItem>
        {
            new("bad_event", 42, 5, DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), null)
        });

        var result = await _controller.IngestBatch(command, CancellationToken.None);

        var bad = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task IngestBatch_WithInvalidClientRequestId_ShouldReturn400()
    {
        var command = new AnalyticsBatchCommand(new List<AnalyticsEventItem>
        {
            new("turn_end", 42, 5, DateTimeOffset.UtcNow, "not-a-guid", null)
        });

        var result = await _controller.IngestBatch(command, CancellationToken.None);

        var bad = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
