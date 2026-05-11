using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SudanDialect.Api.Controllers;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Utilities;
using System.Security.Claims;

namespace SudanDialect.Tests.Unit.Controllers;

public class AdminAuthControllerTests
{
    private readonly Mock<IAdminAuthService> _adminAuthServiceMock;
    private readonly AdminAuthController _sut;

    public AdminAuthControllerTests()
    {
        _adminAuthServiceMock = new Mock<IAdminAuthService>();
        _sut = new AdminAuthController(_adminAuthServiceMock.Object);

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task Login_ShouldReturnOkAndSetCookies_WhenCredentialsAreValid()
    {
        // arrange
        var request = new AdminLoginRequestDto { Username = "admin", Password = "password" };
        var authResult = (
            AccessToken: "access-token",
            AccessExpiresAtUtc: DateTime.UtcNow.AddMinutes(15),
            RefreshToken: "refresh-token",
            RefreshExpiresAtUtc: DateTime.UtcNow.AddDays(7),
            Username: "admin",
            Roles: (IReadOnlyCollection<string>)new[] { "Admin" }
        );

        _adminAuthServiceMock
            .Setup(s => s.LoginAsync(request.Username, request.Password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        // act
        var actionResult = await _sut.Login(request, TestContext.Current.CancellationToken);

        // assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var responseDto = okResult.Value.Should().BeOfType<AdminLoginResponseDto>().Subject;
        responseDto.Username.Should().Be(authResult.Username);

        // Verify cookies in response
        var setCookieHeader = _sut.Response.Headers["Set-Cookie"].ToString();
        setCookieHeader.Should().Contain($"{AdminAuthCookieNames.AccessToken}={authResult.AccessToken}");
        setCookieHeader.Should().Contain($"{AdminAuthCookieNames.RefreshToken}={authResult.RefreshToken}");
        setCookieHeader.Should().Contain("httponly");
        setCookieHeader.Should().Contain("secure");
        setCookieHeader.Should().Contain("samesite=strict");
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenCredentialsAreInvalid()
    {
        // arrange
        var request = new AdminLoginRequestDto { Username = "admin", Password = "wrong" };
        _adminAuthServiceMock
            .Setup(s => s.LoginAsync(request.Username, request.Password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((string, DateTime, string, DateTime, string, IReadOnlyCollection<string>)?)(null));

        // act
        var actionResult = await _sut.Login(request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_ShouldReturnProblem_WhenInvalidOperationExceptionIsThrown()
    {
        // arrange
        var request = new AdminLoginRequestDto { Username = "admin", Password = "password" };
        _adminAuthServiceMock
            .Setup(s => s.LoginAsync(request.Username, request.Password, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("JWT configuration missing"));

        // act
        var actionResult = await _sut.Login(request, TestContext.Current.CancellationToken);

        // assert
        var objectResult = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(500);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Contain("JWT");
    }

    [Fact]
    public async Task Session_ShouldReturnOk_WithSessionDetails()
    {
        // arrange
        var expectedResponse = new AdminLoginResponseDto { Username = "admin", Roles = new[] { "Admin" } };
        _adminAuthServiceMock
            .Setup(s => s.BuildSessionResponseAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // act
        var actionResult = await _sut.Session(TestContext.Current.CancellationToken);

        // assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task Refresh_ShouldReturnUnauthorized_WhenCookieIsMissing()
    {
        // act
        var actionResult = await _sut.Refresh(TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Refresh_ShouldReturnOkAndSetCookies_WhenTokenIsValid()
    {
        // arrange
        _sut.Request.Headers.Append("Cookie", $"{AdminAuthCookieNames.RefreshToken}=valid-token");
        var authResult = (
            AccessToken: "new-access",
            AccessExpiresAtUtc: DateTime.UtcNow.AddMinutes(15),
            RefreshToken: "new-refresh",
            RefreshExpiresAtUtc: DateTime.UtcNow.AddDays(7),
            Username: "admin",
            Roles: (IReadOnlyCollection<string>)new[] { "Admin" }
        );

        _adminAuthServiceMock
            .Setup(s => s.RefreshAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        // act
        var actionResult = await _sut.Refresh(TestContext.Current.CancellationToken);

        // assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var setCookieHeader = _sut.Response.Headers["Set-Cookie"].ToString();
        setCookieHeader.Should().Contain($"{AdminAuthCookieNames.AccessToken}={authResult.AccessToken}");
        setCookieHeader.Should().Contain($"{AdminAuthCookieNames.RefreshToken}={authResult.RefreshToken}");
    }

    [Fact]
    public async Task Refresh_ShouldReturnUnauthorizedAndClearCookies_WhenTokenIsInvalid()
    {
        // arrange
        _sut.Request.Headers.Append("Cookie", $"{AdminAuthCookieNames.RefreshToken}=invalid-token");
        _adminAuthServiceMock
            .Setup(s => s.RefreshAsync("invalid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(((string, DateTime, string, DateTime, string, IReadOnlyCollection<string>)?)(null));

        // act
        var actionResult = await _sut.Refresh(TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().BeOfType<UnauthorizedResult>();
        var setCookieHeader = _sut.Response.Headers["Set-Cookie"].ToString();
        setCookieHeader.Should().Contain($"{AdminAuthCookieNames.AccessToken}=;");
        setCookieHeader.Should().Contain($"{AdminAuthCookieNames.RefreshToken}=;");
        setCookieHeader.Should().Contain("expires=Thu, 01 Jan 1970 00:00:00 GMT");
    }

    [Fact]
    public async Task Refresh_ShouldReturnProblem_WhenInvalidOperationExceptionIsThrown()
    {
        // arrange
        _sut.Request.Headers.Append("Cookie", $"{AdminAuthCookieNames.RefreshToken}=valid-token");
        _adminAuthServiceMock
            .Setup(s => s.RefreshAsync("valid-token", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        // act
        var actionResult = await _sut.Refresh(TestContext.Current.CancellationToken);

        // assert
        var objectResult = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task Logout_ShouldReturnNoContentAndClearCookies()
    {
        // arrange
        _sut.Request.Headers.Append("Cookie", $"{AdminAuthCookieNames.RefreshToken}=token-to-revoke");

        // act
        var actionResult = await _sut.Logout(TestContext.Current.CancellationToken);

        // assert
        actionResult.Should().BeOfType<NoContentResult>();
        _adminAuthServiceMock.Verify(s => s.LogoutAsync(It.IsAny<ClaimsPrincipal>(), "token-to-revoke", It.IsAny<CancellationToken>()), Times.Once);

        var setCookieHeader = _sut.Response.Headers["Set-Cookie"].ToString();
        setCookieHeader.Should().Contain($"{AdminAuthCookieNames.AccessToken}=;");
        setCookieHeader.Should().Contain($"{AdminAuthCookieNames.RefreshToken}=;");
        setCookieHeader.Should().Contain("expires=Thu, 01 Jan 1970 00:00:00 GMT");
    }

    [Fact]
    public async Task Logout_ShouldReturnNoContentAndClearCookies_EvenIfRefreshTokenIsMissing()
    {
        // act
        var actionResult = await _sut.Logout(TestContext.Current.CancellationToken);

        // assert
        actionResult.Should().BeOfType<NoContentResult>();
        _adminAuthServiceMock.Verify(s => s.LogoutAsync(It.IsAny<ClaimsPrincipal>(), null, It.IsAny<CancellationToken>()), Times.Once);

        var setCookieHeader = _sut.Response.Headers["Set-Cookie"].ToString();
        setCookieHeader.Should().Contain($"{AdminAuthCookieNames.AccessToken}=;");
        setCookieHeader.Should().Contain($"{AdminAuthCookieNames.RefreshToken}=;");
    }
}
