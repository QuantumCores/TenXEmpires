using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Controllers;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.DataContracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace TenXEmpires.Server.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAntiforgery> _antiforgeryMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly Mock<SignInManager<IdentityUser<Guid>>> _signInManagerMock;
    private readonly Mock<UserManager<IdentityUser<Guid>>> _userManagerMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _antiforgeryMock = new Mock<IAntiforgery>();
        _loggerMock = new Mock<ILogger<AuthController>>();
        
        // Mock UserManager dependencies
        var userStoreMock = new Mock<IUserStore<IdentityUser<Guid>>>();
        _userManagerMock = new Mock<UserManager<IdentityUser<Guid>>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        
        // Mock SignInManager dependencies
        var contextAccessorMock = new Mock<IHttpContextAccessor>();
        var claimsPrincipalFactoryMock = new Mock<IUserClaimsPrincipalFactory<IdentityUser<Guid>>>();
        _signInManagerMock = new Mock<SignInManager<IdentityUser<Guid>>>(
            _userManagerMock.Object,
            contextAccessorMock.Object,
            claimsPrincipalFactoryMock.Object,
            null!, null!, null!, null!);
        
        _controller = new AuthController(
            _antiforgeryMock.Object, 
            _loggerMock.Object, 
            _signInManagerMock.Object, 
            _userManagerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        // Ensure RequestServices is available to avoid InvalidOperationException in tests
        _controller.HttpContext.RequestServices = new ServiceCollection()
            .BuildServiceProvider();
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

    [Fact]
    public async Task KeepAlive_ShouldReturn204_AndNoStore()
    {
        // Arrange: mark user as authenticated (Authorize attribute is not executed in unit tests)
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "test-user")
        }, authenticationType: "Test");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);

        // Act
        var result = await _controller.KeepAlive();

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _controller.Response.Headers["Cache-Control"].ToString()
            .Should().Contain("no-store");
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturn400_WhenEmailIsNull()
    {
        // Arrange
        var request = new ForgotPasswordRequestDto(Email: null!);

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        var objectResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Code.Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturn400_WhenEmailIsEmpty()
    {
        // Arrange
        var request = new ForgotPasswordRequestDto(Email: string.Empty);

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        var objectResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Code.Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturn204_WhenUserExists()
    {
        // Arrange
        var request = new ForgotPasswordRequestDto(Email: "test@example.com");
        var user = new IdentityUser<Guid>
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            UserName = "test@example.com"
        };

        _userManagerMock.Setup(um => um.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(um => um.GeneratePasswordResetTokenAsync(It.IsAny<IdentityUser<Guid>>()))
            .ReturnsAsync("reset-token-12345");

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _userManagerMock.Verify(um => um.FindByEmailAsync("test@example.com"), Times.Once);
        _userManagerMock.Verify(um => um.GeneratePasswordResetTokenAsync(user), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturn204_WhenUserDoesNotExist()
    {
        // Arrange
        var request = new ForgotPasswordRequestDto(Email: "nonexistent@example.com");

        _userManagerMock.Setup(um => um.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((IdentityUser<Guid>?)null);

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _userManagerMock.Verify(um => um.FindByEmailAsync("nonexistent@example.com"), Times.Once);
        _userManagerMock.Verify(um => um.GeneratePasswordResetTokenAsync(It.IsAny<IdentityUser<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_ShouldLogToken_WhenUserExists()
    {
        // Arrange
        var request = new ForgotPasswordRequestDto(Email: "test@example.com");
        var userId = Guid.NewGuid();
        var user = new IdentityUser<Guid>
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "test@example.com"
        };
        var resetToken = "reset-token-12345";

        _userManagerMock.Setup(um => um.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(um => um.GeneratePasswordResetTokenAsync(It.IsAny<IdentityUser<Guid>>()))
            .ReturnsAsync(resetToken);

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        // Verify logging occurred (check that LogInformation was called)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(userId.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_ShouldLogNonExistentEmail_WhenUserDoesNotExist()
    {
        // Arrange
        var request = new ForgotPasswordRequestDto(Email: "nonexistent@example.com");

        _userManagerMock.Setup(um => um.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((IdentityUser<Guid>?)null);

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        // Verify logging occurred
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("nonexistent@example.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #region Me Endpoint Tests

    [Fact]
    public async Task Me_ShouldReturn401_WhenUserNotAuthenticated()
    {
        // Arrange
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await _controller.Me();

        // Assert
        var objectResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Code.Should().Be("UNAUTHORIZED");
        error.Message.Should().Be("User must be authenticated.");
    }

    [Fact]
    public async Task Me_ShouldReturn401_WhenUserNotFoundInDatabase()
    {
        // Arrange
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, authenticationType: "Test");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);

        _userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((IdentityUser<Guid>?)null);

        // Act
        var result = await _controller.Me();

        // Assert
        var objectResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Code.Should().Be("UNAUTHORIZED");
        error.Message.Should().Be("User not found.");
    }

    [Fact]
    public async Task Me_ShouldReturn200WithUserInfo_WhenAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userEmail = "test@example.com";
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, authenticationType: "Test");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);

        var user = new IdentityUser<Guid>
        {
            Id = userId,
            Email = userEmail,
            UserName = userEmail
        };

        _userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(um => um.GetEmailAsync(user))
            .ReturnsAsync(userEmail);

        // Act
        var result = await _controller.Me();

        // Assert
        var objectResult = result.Should().BeOfType<OkObjectResult>().Subject;
        objectResult.Value.Should().NotBeNull();
    }

    #endregion

    #region Register Endpoint Tests

    [Fact]
    public async Task Register_ShouldReturn400_WhenEmailIsNull()
    {
        // Arrange
        var request = new RegisterRequestDto(Email: null!, Password: "Password123!");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var objectResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Code.Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task Register_ShouldReturn400_WhenPasswordIsEmpty()
    {
        // Arrange
        var request = new RegisterRequestDto(Email: "test@example.com", Password: string.Empty);

        // Act
        var result = await _controller.Register(request);

        // Assert
        var objectResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Code.Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task Register_ShouldReturn400_WhenUserAlreadyExists()
    {
        // Arrange
        var request = new RegisterRequestDto(Email: "existing@example.com", Password: "Password123!");
        var existingUser = new IdentityUser<Guid> { Email = "existing@example.com" };

        _userManagerMock.Setup(um => um.FindByEmailAsync(request.Email))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _controller.Register(request);

        // Assert
        var objectResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Code.Should().Be("USER_EXISTS");
    }

    [Fact]
    public async Task Register_ShouldReturn400_WhenUserCreationFails()
    {
        // Arrange
        var request = new RegisterRequestDto(Email: "test@example.com", Password: "weak");

        _userManagerMock.Setup(um => um.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((IdentityUser<Guid>?)null);
        _userManagerMock.Setup(um => um.CreateAsync(It.IsAny<IdentityUser<Guid>>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        // Act
        var result = await _controller.Register(request);

        // Assert
        var objectResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Code.Should().Be("REGISTRATION_FAILED");
        error.Message.Should().Contain("Password too weak");
    }

    [Fact]
    public async Task Register_ShouldReturn204_WhenRegistrationSucceeds()
    {
        // Arrange
        var request = new RegisterRequestDto(Email: "newuser@example.com", Password: "Password123!");

        _userManagerMock.Setup(um => um.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((IdentityUser<Guid>?)null);
        _userManagerMock.Setup(um => um.CreateAsync(It.IsAny<IdentityUser<Guid>>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _signInManagerMock.Setup(sm => sm.SignInAsync(It.IsAny<IdentityUser<Guid>>(), true, null))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _signInManagerMock.Verify(sm => sm.SignInAsync(It.IsAny<IdentityUser<Guid>>(), true, null), Times.Once);
    }

    #endregion

    #region Login Endpoint Tests

    [Fact]
    public async Task Login_ShouldReturn400_WhenEmailIsNull()
    {
        // Arrange
        var request = new LoginRequestDto(Email: null!, Password: "Password123!");

        // Act
        var result = await _controller.Login(request);

        // Assert
        var objectResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Code.Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task Login_ShouldReturn400_WhenPasswordIsEmpty()
    {
        // Arrange
        var request = new LoginRequestDto(Email: "test@example.com", Password: string.Empty);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var objectResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Code.Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task Login_ShouldReturn400_WhenUserNotFound()
    {
        // Arrange
        var request = new LoginRequestDto(Email: "nonexistent@example.com", Password: "Password123!");

        _userManagerMock.Setup(um => um.FindByEmailAsync(request.Email))
            .ReturnsAsync((IdentityUser<Guid>?)null);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var objectResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_ShouldReturn400_WhenPasswordIsIncorrect()
    {
        // Arrange
        var request = new LoginRequestDto(Email: "test@example.com", Password: "WrongPassword!");
        var user = new IdentityUser<Guid> { Email = request.Email, UserName = request.Email };

        _userManagerMock.Setup(um => um.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(sm => sm.CheckPasswordSignInAsync(user, request.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var objectResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_ShouldReturn204_WhenCredentialsAreValid()
    {
        // Arrange
        var request = new LoginRequestDto(Email: "test@example.com", Password: "Password123!", RememberMe: true);
        var user = new IdentityUser<Guid> { Email = request.Email, UserName = request.Email };

        _userManagerMock.Setup(um => um.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(sm => sm.CheckPasswordSignInAsync(user, request.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _signInManagerMock.Setup(sm => sm.SignInAsync(user, request.RememberMe, null))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _signInManagerMock.Verify(sm => sm.SignInAsync(user, true, null), Times.Once);
    }

    #endregion

    #region Logout Endpoint Tests

    [Fact]
    public async Task Logout_ShouldReturn401_WhenUserNotAuthenticated()
    {
        // Arrange
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await _controller.Logout();

        // Assert
        var objectResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.Code.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Logout_ShouldReturn204_WhenUserIsAuthenticated()
    {
        // Arrange
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "test-user")
        }, authenticationType: "Test");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);

        _signInManagerMock.Setup(sm => sm.SignOutAsync())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Logout();

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _signInManagerMock.Verify(sm => sm.SignOutAsync(), Times.Once);
    }

    #endregion
}
