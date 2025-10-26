using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Controllers;
using TenXEmpires.Server.Domain.Constants;

namespace TenXEmpires.Server.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAntiforgery> _antiforgeryMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _antiforgeryMock = new Mock<IAntiforgery>();
        _loggerMock = new Mock<ILogger<AuthController>>();
        _controller = new AuthController(_antiforgeryMock.Object, _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void GetCsrfToken_ShouldReturn204_AndSetCookie()
    {
        var tokens = new AntiforgeryTokenSet(
            requestToken: "test-token",
            cookieToken: "cookie-token",
            formFieldName: "__RequestVerificationToken",
            headerName: SecurityConstants.XsrfHeader);

        _antiforgeryMock.Setup(a => a.GetAndStoreTokens(It.IsAny<HttpContext>()))
            .Returns(tokens);

        var result = _controller.GetCsrfToken();

        result.Should().BeOfType<NoContentResult>();

        var setCookie = _controller.Response.Headers["Set-Cookie"].ToString();
        setCookie.Should().Contain($"{SecurityConstants.XsrfCookie}=test-token");
        setCookie.Should().ContainEquivalentOf("Secure");
        setCookie.Should().ContainEquivalentOf("Path=/");
        setCookie.Should().ContainEquivalentOf("SameSite=Lax");

        _controller.Response.Headers["Cache-Control"].ToString()
            .Should().Contain("no-store");
    }

    [Fact]
    public void GetCsrfToken_ShouldReturn500_WhenTokenMissing()
    {
        var tokens = new AntiforgeryTokenSet(
            requestToken: string.Empty,
            cookieToken: "cookie-token",
            formFieldName: "__RequestVerificationToken",
            headerName: SecurityConstants.XsrfHeader);

        _antiforgeryMock.Setup(a => a.GetAndStoreTokens(It.IsAny<HttpContext>()))
            .Returns(tokens);

        var result = _controller.GetCsrfToken();

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
