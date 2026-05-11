using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SudanDialect.Api.Configuration;
using SudanDialect.Api.Services;
using System.Security.Claims;

namespace SudanDialect.Tests.Unit.Services;

public class AdminAuthServiceTests
{
    private readonly Mock<UserManager<IdentityUser>> _userManagerMock;
    private readonly Mock<SignInManager<IdentityUser>> _signInManagerMock;
    private readonly Mock<IOptions<JwtOptions>> _jwtOptionsMock;
    private readonly AdminAuthService _sut;

    public AdminAuthServiceTests()
    {
        _userManagerMock = MockHelpers.MockUserManager<IdentityUser>();
        _signInManagerMock = MockHelpers.MockSignInManager<IdentityUser>(_userManagerMock.Object);

        _jwtOptionsMock = new Mock<IOptions<JwtOptions>>();
        _jwtOptionsMock.Setup(o => o.Value).Returns(new JwtOptions
        {
            SigningKey = "very-long-secret-key-that-is-at-least-32-chars",
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        });

        _sut = new AdminAuthService(_userManagerMock.Object, _signInManagerMock.Object, _jwtOptionsMock.Object);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenUserNotFound()
    {
        // arrange
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync((IdentityUser?)null);

        // act
        var result = await _sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);

        // assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnAuthResult_WhenCredentialsAreValid()
    {
        // arrange
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, "password", false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        // act
        var result = await _sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);

        // assert
        result.Should().NotBeNull();
        var authResult = result!.Value;
        authResult.Username.Should().Be("admin");
        authResult.Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task LogoutAsync_ShouldRemoveToken_WhenUserIsAuthenticated()
    {
        // arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1")
        }, "mock"));
        var identityUser = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(identityUser);

        // act
        await _sut.LogoutAsync(user, "refresh-token", TestContext.Current.CancellationToken);

        // assert
        _userManagerMock.Verify(u => u.RemoveAuthenticationTokenAsync(identityUser, "SudanDialect.Admin", "RefreshToken"), Times.Once);
    }
}
