using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SudanDialect.Api.Configuration;
using SudanDialect.Api.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace SudanDialect.Tests.Unit.Services;

public class AdminAuthServiceTests
{
    private readonly Mock<UserManager<IdentityUser>> _userManagerMock;
    private readonly Mock<SignInManager<IdentityUser>> _signInManagerMock;
    private readonly Mock<IOptions<JwtOptions>> _jwtOptionsMock;
    private readonly AdminAuthService _sut;
    private readonly JwtOptions _jwtOptions;

    public AdminAuthServiceTests()
    {
        _userManagerMock = MockHelpers.MockUserManager<IdentityUser>();
        _signInManagerMock = MockHelpers.MockSignInManager<IdentityUser>(_userManagerMock.Object);

        _jwtOptions = new JwtOptions
        {
            SigningKey = "very-long-secret-key-that-is-at-least-32-chars",
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        };

        _jwtOptionsMock = new Mock<IOptions<JwtOptions>>();
        _jwtOptionsMock.Setup(o => o.Value).Returns(_jwtOptions);

        _sut = new AdminAuthService(_userManagerMock.Object, _signInManagerMock.Object, _jwtOptionsMock.Object);
    }

    #region LoginAsync Tests

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenUserNotFound()
    {
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync((IdentityUser?)null);

        var result = await _sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnAuthResult_WhenCredentialsAreValid()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, "password", false))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        var authResult = result!.Value;
        authResult.Username.Should().Be("admin");
        authResult.Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenCredentialsAreNotValid()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, "password", false))
            .ReturnsAsync(SignInResult.Failed);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenPasswordIsNull()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, null!, false))
            .ReturnsAsync(SignInResult.Failed);

        var result = await _sut.LoginAsync("admin", null!, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenPasswordIsEmpty()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, string.Empty, false))
            .ReturnsAsync(SignInResult.Failed);

        var result = await _sut.LoginAsync("admin", string.Empty, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ShouldTrimUsername_WithLeadingAndTrailingWhitespace()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, "password", false))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.LoginAsync("  admin  ", "password", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.Username.Should().Be("admin");
    }

    [Fact]
    public async Task LoginAsync_ShouldUseFallbackUsername_WhenUserNameIsNull()
    {
        var user = new IdentityUser { Id = "1", UserName = null };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, "password", false))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.Username.Should().Be("admin");
    }

[Fact]
    public async Task LoginAsync_ShouldAssignDefaultModeratorRole_WhenUserHasNoRoles()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, "password", false))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        _userManagerMock.Setup(u => u.AddToRoleAsync(user, "moderator"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.Roles.Should().Contain("moderator");
        _userManagerMock.Verify(u => u.AddToRoleAsync(user, "moderator"), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnModeratorRole_WhenAddToRoleFails()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, "password", false))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        _userManagerMock.Setup(u => u.AddToRoleAsync(user, "moderator"))
            .ReturnsAsync(IdentityResult.Failed());
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.Roles.Should().Contain("moderator");
    }

    #endregion

    #region BuildSessionResponseAsync Tests

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldReturnUsernameFromNameClaim()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "adminuser"),
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));

        var result = await _sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        result.Username.Should().Be("adminuser");
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldFallbackToUniqueNameClaim_WhenNameClaimMissing()
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.UniqueName, "uniqueadmin"),
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));

        var result = await _sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        result.Username.Should().Be("uniqueadmin");
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldReturnEmptyString_WhenNoUsernameClaimExists()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));

        var result = await _sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        result.Username.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldExtractRolesFromTokenClaims()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Moderator")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));

        var result = await _sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        result.Roles.Should().Contain("Admin");
        result.Roles.Should().Contain("Moderator");
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldDeduplicateRoles()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(ClaimTypes.Role, "ADMIN")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));

        var result = await _sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        result.Roles.Count.Should().Be(1);
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldFetchRolesFromDb_WhenNoRoleClaimsInToken()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.NameIdentifier, "1")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));
        var identityUser = new IdentityUser { Id = "1", UserName = "admin" };

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(identityUser);
        _userManagerMock.Setup(u => u.GetRolesAsync(identityUser)).ReturnsAsync(new List<string> { "Admin" });

        var result = await _sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        result.Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldReturnDefaultModeratorRole_WhenDbUserHasNoRoles()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.NameIdentifier, "1")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));
        var identityUser = new IdentityUser { Id = "1", UserName = "admin" };

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(identityUser);
        _userManagerMock.Setup(u => u.GetRolesAsync(identityUser)).ReturnsAsync(new List<string>());
        _userManagerMock.Setup(u => u.AddToRoleAsync(identityUser, "moderator"))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        result.Roles.Should().Contain("moderator");
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldReturnModeratorRole_WhenAddToRoleFails()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.NameIdentifier, "1")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));
        var identityUser = new IdentityUser { Id = "1", UserName = "admin" };

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(identityUser);
        _userManagerMock.Setup(u => u.GetRolesAsync(identityUser)).ReturnsAsync(new List<string>());
        _userManagerMock.Setup(u => u.AddToRoleAsync(identityUser, "moderator"))
            .ReturnsAsync(IdentityResult.Failed());

        var result = await _sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        result.Roles.Should().Contain("moderator");
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldReturnEmptyRoles_WhenUserNotFoundInFallback()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.NameIdentifier, "999")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));

        _userManagerMock.Setup(u => u.FindByIdAsync("999")).ReturnsAsync((IdentityUser?)null);

        var result = await _sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        result.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldParseExpirationFromTokenClaim()
    {
        var expiration = DateTimeOffset.UtcNow.AddHours(1);
        var expUnixSeconds = expiration.ToUnixTimeSeconds();
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(JwtRegisteredClaimNames.Exp, expUnixSeconds.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));

        var result = await _sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        result.ExpiresAtUtc.Should().BeCloseTo(expiration.UtcDateTime, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldFallbackToCalculatedExpiration_WhenExpClaimMissing()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.NameIdentifier, "1")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));
        var beforeCall = DateTime.UtcNow;

        var result = await _sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        var afterCall = DateTime.UtcNow;
        result.ExpiresAtUtc.Should().BeAfter(beforeCall.AddMinutes(14));
        result.ExpiresAtUtc.Should().BeBefore(afterCall.AddMinutes(16));
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldThrow_WhenCancellationTokenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var claims = new[] { new Claim(ClaimTypes.Name, "admin") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));

        var act = () => _sut.BuildSessionResponseAsync(principal, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldFilterOutWhitespaceOnlyRoles()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "   ")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));

        var result = await _sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        result.Roles.Should().Contain("Admin");
        result.Roles.Should().NotContain("   ");
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldFallbackToSubClaim_WhenNameIdentifierMissing()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(JwtRegisteredClaimNames.Sub, "1")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));
        var identityUser = new IdentityUser { Id = "1", UserName = "admin" };

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(identityUser);
        _userManagerMock.Setup(u => u.GetRolesAsync(identityUser)).ReturnsAsync(new List<string> { "Admin" });

        var result = await _sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        result.Roles.Should().Contain("Admin");
    }

    #endregion

    #region RefreshAsync Tests

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenRefreshTokenIsNull()
    {
        var result = await _sut.RefreshAsync(null!, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenRefreshTokenIsEmpty()
    {
        var result = await _sut.RefreshAsync(string.Empty, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenRefreshTokenIsWhitespace()
    {
        var result = await _sut.RefreshAsync("   ", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenRefreshTokenHasNoSeparator()
    {
        var result = await _sut.RefreshAsync("no-separator-here", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenRefreshTokenUserIdIsEmpty()
    {
        var result = await _sut.RefreshAsync(":somebase64data", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenUserNotFoundByUserId()
    {
        _userManagerMock.Setup(u => u.FindByIdAsync("999")).ReturnsAsync((IdentityUser?)null);

        var result = await _sut.RefreshAsync("999:somebase64data", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenStoredRefreshTokenIsMissing()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync((string?)null);

        var result = await _sut.RefreshAsync("1:somebase64data", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenStoredRefreshTokenIsEmpty()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync(string.Empty);

        var result = await _sut.RefreshAsync("1:somebase64data", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenStoredRefreshTokenIsExpired()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        var expiredPayload = JsonSerializer.Serialize(new
        {
            TokenHash = "dummyhash",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(-1)
        });

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync(expiredPayload);

        var result = await _sut.RefreshAsync("1:somebase64data", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenTokenHashDoesNotMatch()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        var validPayload = JsonSerializer.Serialize(new
        {
            TokenHash = "differenthash",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync(validPayload);

        var result = await _sut.RefreshAsync("1:somebase64data", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldRemoveOldTokenOnValidationFailure()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        var validPayload = JsonSerializer.Serialize(new
        {
            TokenHash = "differenthash",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync(validPayload);

        await _sut.RefreshAsync("1:somebase64data", TestContext.Current.CancellationToken);

        _userManagerMock.Verify(u => u.RemoveAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNewTokens_WhenRefreshSucceeds()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        var refreshToken = CreateRefreshTokenForUser("1");
        var tokenHash = ComputeSha256(refreshToken);
        var storedPayload = JsonSerializer.Serialize(new
        {
            TokenHash = tokenHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync(storedPayload);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.RefreshAsync(refreshToken, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.AccessToken.Should().NotBeEmpty();
        result.Value.RefreshToken.Should().NotBeEmpty();
        result.Value.Username.Should().Be("admin");
    }

    [Fact]
    public async Task RefreshAsync_ShouldRotateRefreshToken_OnSuccessfulRefresh()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        var oldRefreshToken = CreateRefreshTokenForUser("1");
        var tokenHash = ComputeSha256(oldRefreshToken);
        var storedPayload = JsonSerializer.Serialize(new
        {
            TokenHash = tokenHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });

        string? capturedNewToken = null;
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync(storedPayload);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success)
            .Callback((IdentityUser _, string _, string _, string newToken) => capturedNewToken = newToken);

        var result = await _sut.RefreshAsync(oldRefreshToken, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.RefreshToken.Should().NotBe(oldRefreshToken);
        capturedNewToken.Should().NotBeNull();
        var newPayload = JsonSerializer.Deserialize<StoredRefreshTokenInfo>(capturedNewToken!);
        newPayload!.TokenHash.Should().NotBe(tokenHash);
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnCorrectUserInfo_InSuccessfulResponse()
    {
        var user = new IdentityUser { Id = "1", UserName = "testadmin" };
        var refreshToken = CreateRefreshTokenForUser("1");
        var tokenHash = ComputeSha256(refreshToken);
        var storedPayload = JsonSerializer.Serialize(new
        {
            TokenHash = tokenHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync(storedPayload);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin", "Moderator" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.RefreshAsync(refreshToken, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.Username.Should().Be("testadmin");
        result.Value.Roles.Should().Contain("Admin");
        result.Value.Roles.Should().Contain("Moderator");
    }

    [Fact]
    public async Task RefreshAsync_ShouldThrow_WhenCancellationTokenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _sut.RefreshAsync("1:somebase64data", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RefreshAsync_ShouldUseUserIdAsUsername_WhenUserNameIsNull()
    {
        var user = new IdentityUser { Id = "1", UserName = null };
        var refreshToken = CreateRefreshTokenForUser("1");
        var tokenHash = ComputeSha256(refreshToken);
        var storedPayload = JsonSerializer.Serialize(new
        {
            TokenHash = tokenHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync(storedPayload);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.RefreshAsync(refreshToken, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.Username.Should().Be("1");
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenStoredTokenHashIsEmpty()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        var storedPayload = JsonSerializer.Serialize(new
        {
            TokenHash = "",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync(storedPayload);

        var result = await _sut.RefreshAsync("1:somebase64data", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenStoredTokenHashIsNull()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        var storedPayload = JsonSerializer.Serialize(new
        {
            TokenHash = (string?)null,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync(storedPayload);

        var result = await _sut.RefreshAsync("1:somebase64data", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenStoredPayloadIsNull()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync("null");

        var result = await _sut.RefreshAsync("1:somebase64data", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenStoredTokenJsonIsMalformed()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync("not-valid-json{");

        var result = await _sut.RefreshAsync("1:somebase64data", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldThrow_WhenSigningKeyNotConfigured()
    {
        var jwtOptions = new JwtOptions
        {
            SigningKey = "", // empty signing key
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        };
        var jwtOptionsMock = new Mock<IOptions<JwtOptions>>();
        jwtOptionsMock.Setup(o => o.Value).Returns(jwtOptions);
        var sut = new AdminAuthService(_userManagerMock.Object, _signInManagerMock.Object, jwtOptionsMock.Object);

        var act = () => sut.RefreshAsync("1:somebase64data", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("JWT configuration is missing");
    }

    [Fact]
    public async Task LoginAsync_ShouldThrow_WhenSigningKeyNotConfigured()
    {
        var jwtOptions = new JwtOptions
        {
            SigningKey = "", // empty signing key
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        };
        var jwtOptionsMock = new Mock<IOptions<JwtOptions>>();
        jwtOptionsMock.Setup(o => o.Value).Returns(jwtOptions);
        var sut = new AdminAuthService(_userManagerMock.Object, _signInManagerMock.Object, jwtOptionsMock.Object);

        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, "password", false))
            .ReturnsAsync(SignInResult.Success);

        var act = () => sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("JWT configuration is missing");
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnNull_WhenRefreshTokenSeparatorAtEnd()
    {
        var result = await _sut.RefreshAsync("userid:", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LogoutAsync_ShouldDoNothing_WhenRefreshTokenSeparatorAtEnd()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        await _sut.LogoutAsync(user, "userid:", TestContext.Current.CancellationToken);

        _userManagerMock.Verify(u => u.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ShouldFail_WhenSetAuthenticationTokenFails()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, "password", false))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed());

        var result = await _sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldFail_WhenSetAuthenticationTokenFails()
    {
        var user = new IdentityUser { Id = "1", UserName = "admin" };
        var refreshToken = CreateRefreshTokenForUser("1");
        var tokenHash = ComputeSha256(refreshToken);
        var storedPayload = JsonSerializer.Serialize(new
        {
            TokenHash = tokenHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken"))
            .ReturnsAsync(storedPayload);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed());

        var result = await _sut.RefreshAsync(refreshToken, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
    }

    #endregion

    #region LogoutAsync Tests

    [Fact]
    public async Task LogoutAsync_ShouldRemoveToken_WhenUserIsAuthenticated()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1")
        }, "mock"));
        var identityUser = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(identityUser);

        await _sut.LogoutAsync(user, "refresh-token", TestContext.Current.CancellationToken);

        _userManagerMock.Verify(u => u.RemoveAuthenticationTokenAsync(identityUser, "SudanDialect.Admin", "RefreshToken"), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_ShouldDoNothing_WhenUserNotAuthenticatedAndNoRefreshToken()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        await _sut.LogoutAsync(user, null, TestContext.Current.CancellationToken);
        await _sut.LogoutAsync(user, string.Empty, TestContext.Current.CancellationToken);
        await _sut.LogoutAsync(user, "   ", TestContext.Current.CancellationToken);

        _userManagerMock.Verify(u => u.RemoveAuthenticationTokenAsync(It.IsAny<IdentityUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LogoutAsync_ShouldRemoveTokenByRefreshToken_WhenUserNotAuthenticated()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var refreshToken = CreateRefreshTokenForUser("1");
        var identityUser = new IdentityUser { Id = "1", UserName = "admin" };

        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(identityUser);

        await _sut.LogoutAsync(user, refreshToken, TestContext.Current.CancellationToken);

        _userManagerMock.Verify(u => u.RemoveAuthenticationTokenAsync(identityUser, "SudanDialect.Admin", "RefreshToken"), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_ShouldDoNothing_WhenRefreshTokenFormatInvalid()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        await _sut.LogoutAsync(user, "no-separator", TestContext.Current.CancellationToken);

        _userManagerMock.Verify(u => u.RemoveAuthenticationTokenAsync(It.IsAny<IdentityUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LogoutAsync_ShouldDoNothing_WhenRefreshTokenUserIdNotFound()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        _userManagerMock.Setup(u => u.FindByIdAsync("999")).ReturnsAsync((IdentityUser?)null);

        await _sut.LogoutAsync(user, "999:somebase64data", TestContext.Current.CancellationToken);

        _userManagerMock.Verify(u => u.RemoveAuthenticationTokenAsync(It.IsAny<IdentityUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LogoutAsync_ShouldThrow_WhenCancellationTokenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        var act = () => _sut.LogoutAsync(user, "refresh-token", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LogoutAsync_ShouldDoNothing_WhenAuthenticatedButNoNameIdentifierClaim()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "admin")
        }, "mock");
        identity.AddClaim(new Claim(ClaimTypes.AuthenticationMethod, "mock"));
        var user = new ClaimsPrincipal(identity);

        await _sut.LogoutAsync(user, "refresh-token", TestContext.Current.CancellationToken);

        _userManagerMock.Verify(u => u.FindByIdAsync(It.IsAny<string>()), Times.Never);
        _userManagerMock.Verify(u => u.RemoveAuthenticationTokenAsync(It.IsAny<IdentityUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LogoutAsync_ShouldDoNothing_WhenAuthenticatedUserIdIsWhitespace()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "   ")
        }, "mock");
        identity.AddClaim(new Claim(ClaimTypes.AuthenticationMethod, "mock"));
        var user = new ClaimsPrincipal(identity);

        await _sut.LogoutAsync(user, "refresh-token", TestContext.Current.CancellationToken);

        _userManagerMock.Verify(u => u.FindByIdAsync(It.IsAny<string>()), Times.Never);
        _userManagerMock.Verify(u => u.RemoveAuthenticationTokenAsync(It.IsAny<IdentityUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region JwtOptions Edge Cases Tests

    [Fact]
    public async Task LoginAsync_ShouldUseMinExpirationMinutes_WhenConfiguredLessThan10()
    {
        var jwtOptions = new JwtOptions
        {
            SigningKey = "very-long-secret-key-that-is-at-least-32-chars",
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 5,
            RefreshTokenExpirationDays = 7
        };
        var jwtOptionsMock = new Mock<IOptions<JwtOptions>>();
        jwtOptionsMock.Setup(o => o.Value).Returns(jwtOptions);
        var sut = new AdminAuthService(_userManagerMock.Object, _signInManagerMock.Object, jwtOptionsMock.Object);

        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, "password", false))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var beforeCall = DateTime.UtcNow;
        var result = await sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);
        var afterCall = DateTime.UtcNow;

        result.Should().NotBeNull();
        result!.Value.AccessExpiresAtUtc.Should().BeAfter(beforeCall.AddMinutes(9));
        result.Value.AccessExpiresAtUtc.Should().BeBefore(afterCall.AddMinutes(11));
    }

    [Fact]
    public async Task LoginAsync_ShouldUseMaxExpirationMinutes_WhenConfiguredGreaterThan15()
    {
        var jwtOptions = new JwtOptions
        {
            SigningKey = "very-long-secret-key-that-is-at-least-32-chars",
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 30,
            RefreshTokenExpirationDays = 7
        };
        var jwtOptionsMock = new Mock<IOptions<JwtOptions>>();
        jwtOptionsMock.Setup(o => o.Value).Returns(jwtOptions);
        var sut = new AdminAuthService(_userManagerMock.Object, _signInManagerMock.Object, jwtOptionsMock.Object);

        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, "password", false))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var beforeCall = DateTime.UtcNow;
        var result = await sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);
        var afterCall = DateTime.UtcNow;

        result.Should().NotBeNull();
        result!.Value.AccessExpiresAtUtc.Should().BeAfter(beforeCall.AddMinutes(14));
        result.Value.AccessExpiresAtUtc.Should().BeBefore(afterCall.AddMinutes(16));
    }

    [Fact]
    public async Task LoginAsync_ShouldUseMinRefreshTokenDays_WhenConfiguredToZero()
    {
        var jwtOptions = new JwtOptions
        {
            SigningKey = "very-long-secret-key-that-is-at-least-32-chars",
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 15,
            RefreshTokenExpirationDays = 0
        };
        var jwtOptionsMock = new Mock<IOptions<JwtOptions>>();
        jwtOptionsMock.Setup(o => o.Value).Returns(jwtOptions);
        var sut = new AdminAuthService(_userManagerMock.Object, _signInManagerMock.Object, jwtOptionsMock.Object);

        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, "password", false))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.RefreshExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddHours(23));
    }

    [Fact]
    public async Task LoginAsync_ShouldUseMinRefreshTokenDays_WhenConfiguredToOne()
    {
        var jwtOptions = new JwtOptions
        {
            SigningKey = "very-long-secret-key-that-is-at-least-32-chars",
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 15,
            RefreshTokenExpirationDays = 1
        };
        var jwtOptionsMock = new Mock<IOptions<JwtOptions>>();
        jwtOptionsMock.Setup(o => o.Value).Returns(jwtOptions);
        var sut = new AdminAuthService(_userManagerMock.Object, _signInManagerMock.Object, jwtOptionsMock.Object);

        var user = new IdentityUser { Id = "1", UserName = "admin" };
        _userManagerMock.Setup(u => u.FindByNameAsync("admin")).ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.CheckPasswordSignInAsync(user, "password", false))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(u => u.SetAuthenticationTokenAsync(user, "SudanDialect.Admin", "RefreshToken", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await sut.LoginAsync("admin", "password", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.RefreshExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddHours(23));
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldUseClampedExpiration_WhenExpirationMinutesBelow10()
    {
        var jwtOptions = new JwtOptions
        {
            SigningKey = "very-long-secret-key-that-is-at-least-32-chars",
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 5,
            RefreshTokenExpirationDays = 7
        };
        var jwtOptionsMock = new Mock<IOptions<JwtOptions>>();
        jwtOptionsMock.Setup(o => o.Value).Returns(jwtOptions);
        var sut = new AdminAuthService(_userManagerMock.Object, _signInManagerMock.Object, jwtOptionsMock.Object);

        var claims = new[] { new Claim(ClaimTypes.Name, "admin") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));
        var beforeCall = DateTime.UtcNow;

        var result = await sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        var afterCall = DateTime.UtcNow;
        result.ExpiresAtUtc.Should().BeAfter(beforeCall.AddMinutes(9));
        result.ExpiresAtUtc.Should().BeBefore(afterCall.AddMinutes(11));
    }

    [Fact]
    public async Task BuildSessionResponseAsync_ShouldUseClampedExpiration_WhenExpirationMinutesAbove15()
    {
        var jwtOptions = new JwtOptions
        {
            SigningKey = "very-long-secret-key-that-is-at-least-32-chars",
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 30,
            RefreshTokenExpirationDays = 7
        };
        var jwtOptionsMock = new Mock<IOptions<JwtOptions>>();
        jwtOptionsMock.Setup(o => o.Value).Returns(jwtOptions);
        var sut = new AdminAuthService(_userManagerMock.Object, _signInManagerMock.Object, jwtOptionsMock.Object);

        var claims = new[] { new Claim(ClaimTypes.Name, "admin") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));
        var beforeCall = DateTime.UtcNow;

        var result = await sut.BuildSessionResponseAsync(principal, TestContext.Current.CancellationToken);

        var afterCall = DateTime.UtcNow;
        result.ExpiresAtUtc.Should().BeAfter(beforeCall.AddMinutes(14));
        result.ExpiresAtUtc.Should().BeBefore(afterCall.AddMinutes(16));
    }

    #endregion

    private static string CreateRefreshTokenForUser(string userId)
    {
        var nonce = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        return $"{userId}:{nonce}";
    }

    private static string ComputeSha256(string input)
    {
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes);
    }

    private record StoredRefreshTokenInfo(string TokenHash, DateTime ExpiresAtUtc);
}